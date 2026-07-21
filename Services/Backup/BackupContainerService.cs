using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Reads and writes the .devbackup binary container format.
    ///
    /// FIXED HEADER (40 bytes):
    ///   [0..7]   MAGIC        "DEVBACKU" (8 bytes ASCII)
    ///   [8..9]   VERSION      uint16 LE
    ///   [10..11] FLAGS        uint16 LE (reserved)
    ///   [12..15] METADATA_LEN uint32 LE
    ///   [16..19] MANIFEST_LEN uint32 LE
    ///   [20..23] CHECKSUMS_LEN uint32 LE
    ///   [24..27] SESSKEY_LEN  uint32 LE
    ///   [28..35] PAYLOAD_LEN  uint64 LE
    ///   [36..39] SIG_LEN      uint32 LE
    ///
    /// SECTIONS (immediately after header, in order):
    ///   metadata.json   (unencrypted, UTF-8)
    ///   manifest.json   (unencrypted, UTF-8)
    ///   checksums.json  (unencrypted, UTF-8)
    ///   encrypted.session.key
    ///   encrypted.payload
    ///   signature.bin   (HMAC-SHA256 over all preceding bytes)
    /// </summary>
    public sealed class BackupContainerService
    {
        private static readonly Lazy<BackupContainerService> _instance =
            new(() => new BackupContainerService(), true);
        public static BackupContainerService Instance => _instance.Value;
        private BackupContainerService() { }

        // ── Magic constant ────────────────────────────────────────────
        public static readonly byte[] Magic =
            Encoding.ASCII.GetBytes("DEVBACKU"); // 8 bytes

        public const ushort CurrentVersion = 1;
        private const int   FixedHeaderSize = 40;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented          = true,
            PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
        };

        // ── Write ─────────────────────────────────────────────────────

        /// <summary>
        /// Writes the full .devbackup container to a file.
        /// The encrypted payload stream is read once — no intermediate temp file.
        /// The signature is computed over all bytes BEFORE the signature section
        /// by seeking back and overwriting the reserved sig field.
        /// </summary>
        public async Task WriteContainerAsync(
            string outputPath,
            BackupMetadata  metadata,
            BackupManifest  manifest,
            ChecksumManifest checksums,
            byte[]          encryptedSessionKey,
            Stream          encryptedPayload,
            byte[]          wrappingKey,
            CancellationToken ct = default)
        {
            // Serialize unencrypted sections
            byte[] metaBytes     = JsonBytes(metadata);
            byte[] manifestBytes = JsonBytes(manifest);
            byte[] checkBytes    = JsonBytes(checksums);
            long   payloadLen    = encryptedPayload.Length - encryptedPayload.Position;

            // Compute signature length (always 32 for HMAC-SHA256)
            const int sigLen = 32;

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

            await using var fs = new FileStream(
                outputPath, FileMode.Create, FileAccess.ReadWrite,
                FileShare.None, 81920, useAsync: true);

            // ── Write fixed header ────────────────────────────────────
            byte[] header = BuildHeader(
                (uint)metaBytes.Length,
                (uint)manifestBytes.Length,
                (uint)checkBytes.Length,
                (uint)encryptedSessionKey.Length,
                (ulong)payloadLen,
                (uint)sigLen);

            await fs.WriteAsync(header, ct).ConfigureAwait(false);

            // ── Write sections ────────────────────────────────────────
            await fs.WriteAsync(metaBytes,             ct).ConfigureAwait(false);
            await fs.WriteAsync(manifestBytes,          ct).ConfigureAwait(false);
            await fs.WriteAsync(checkBytes,             ct).ConfigureAwait(false);
            await fs.WriteAsync(encryptedSessionKey,    ct).ConfigureAwait(false);

            // Stream payload (may be large)
            await encryptedPayload.CopyToAsync(fs, 81920, ct).ConfigureAwait(false);

            // ── Compute and append signature ──────────────────────────
            long sigOffset = fs.Position;
            fs.Position = 0;
            byte[] sig = ChecksumService.Instance
                .ComputeContainerSignatureFromStream(wrappingKey, fs);

            // Move back to end and append signature
            fs.Position = sigOffset;
            await fs.WriteAsync(sig, ct).ConfigureAwait(false);

            await fs.FlushAsync(ct).ConfigureAwait(false);
        }

        // ── Read (header + unencrypted sections only) ─────────────────

        /// <summary>
        /// Reads ONLY the unencrypted header sections from a .devbackup file.
        /// Does NOT decrypt or load the payload — safe for preview/validation.
        /// </summary>
        public async Task<ContainerHeader> ReadHeaderAsync(
            string filePath, CancellationToken ct = default)
        {
            await using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, useAsync: true);

            return await ReadHeaderFromStreamAsync(fs, ct).ConfigureAwait(false);
        }

        public async Task<ContainerHeader> ReadHeaderFromStreamAsync(
            Stream fs, CancellationToken ct = default)
        {
            byte[] headerBuf = new byte[FixedHeaderSize];
            int read = await ReadExactAsync(fs, headerBuf, 0, FixedHeaderSize, ct)
                .ConfigureAwait(false);
            if (read < FixedHeaderSize)
                throw new InvalidDataException("File is too short to be a .devbackup container.");

            // Validate magic
            for (int i = 0; i < 8; i++)
                if (headerBuf[i] != Magic[i])
                    throw new InvalidDataException("Not a .devbackup file (magic mismatch).");

            ushort version      = BitConverter.ToUInt16(headerBuf, 8);
            uint   metaLen      = BitConverter.ToUInt32(headerBuf, 12);
            uint   manifestLen  = BitConverter.ToUInt32(headerBuf, 16);
            uint   checksumLen  = BitConverter.ToUInt32(headerBuf, 20);
            uint   sessKeyLen   = BitConverter.ToUInt32(headerBuf, 24);
            ulong  payloadLen   = BitConverter.ToUInt64(headerBuf, 28);
            uint   sigLen       = BitConverter.ToUInt32(headerBuf, 36);

            // Read unencrypted sections
            byte[] metaBytes     = await ReadBytesAsync(fs, (int)metaLen,     ct);
            byte[] manifestBytes = await ReadBytesAsync(fs, (int)manifestLen, ct);
            byte[] checksumBytes = await ReadBytesAsync(fs, (int)checksumLen, ct);

            var metadata  = JsonDeserialize<BackupMetadata>(metaBytes);
            var manifest  = JsonDeserialize<BackupManifest>(manifestBytes);
            var checksums = JsonDeserialize<ChecksumManifest>(checksumBytes);

            // Record stream positions for payload/sig access
            long sessKeyOffset = fs.Position;
            long payloadOffset = sessKeyOffset + sessKeyLen;
            long sigOffset     = payloadOffset + (long)payloadLen;

            return new ContainerHeader
            {
                Version            = version,
                Metadata           = metadata,
                Manifest           = manifest,
                Checksums          = checksums,
                SessionKeyLength   = (int)sessKeyLen,
                PayloadLength      = (long)payloadLen,
                SignatureLength    = (int)sigLen,
                SessionKeyOffset   = sessKeyOffset,
                PayloadOffset      = payloadOffset,
                SignatureOffset    = sigOffset,
                TotalFileSize      = sigOffset + sigLen
            };
        }

        /// <summary>
        /// Reads the encrypted session key blob from a container.
        /// </summary>
        public async Task<byte[]> ReadEncryptedSessionKeyAsync(
            string filePath, ContainerHeader header,
            CancellationToken ct = default)
        {
            await using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 4096, useAsync: true);

            fs.Seek(header.SessionKeyOffset, SeekOrigin.Begin);
            return await ReadBytesAsync(fs, header.SessionKeyLength, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Returns a positioned read-only stream over the encrypted payload section.
        /// Caller must dispose.
        /// </summary>
        public FileStream OpenPayloadStream(string filePath, ContainerHeader header)
        {
            var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, useAsync: true);
            fs.Seek(header.PayloadOffset, SeekOrigin.Begin);
            return fs;
        }

        /// <summary>
        /// Reads the signature bytes from the container.
        /// </summary>
        public async Task<byte[]> ReadSignatureAsync(
            string filePath, ContainerHeader header,
            CancellationToken ct = default)
        {
            await using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 64, useAsync: true);
            fs.Seek(header.SignatureOffset, SeekOrigin.Begin);
            return await ReadBytesAsync(fs, header.SignatureLength, ct)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Opens the container and returns a stream of all bytes BEFORE the signature,
        /// used for HMAC verification.
        /// </summary>
        public async Task<byte[]> ReadPreSignatureBytesAsync(
            string filePath, ContainerHeader header,
            CancellationToken ct = default)
        {
            await using var fs = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 81920, useAsync: true);

            byte[] buf = new byte[header.SignatureOffset];
            await ReadExactAsync(fs, buf, 0, (int)header.SignatureOffset, ct)
                .ConfigureAwait(false);
            return buf;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static byte[] BuildHeader(
            uint metaLen, uint manifestLen, uint checkLen,
            uint sessKeyLen, ulong payloadLen, uint sigLen)
        {
            byte[] h = new byte[FixedHeaderSize];
            Array.Copy(Magic, h, 8);
            Array.Copy(BitConverter.GetBytes(CurrentVersion), 0, h, 8,  2);
            // flags at [10..11] = 0 (reserved)
            Array.Copy(BitConverter.GetBytes(metaLen),    0, h, 12, 4);
            Array.Copy(BitConverter.GetBytes(manifestLen), 0, h, 16, 4);
            Array.Copy(BitConverter.GetBytes(checkLen),   0, h, 20, 4);
            Array.Copy(BitConverter.GetBytes(sessKeyLen), 0, h, 24, 4);
            Array.Copy(BitConverter.GetBytes(payloadLen), 0, h, 28, 8);
            Array.Copy(BitConverter.GetBytes(sigLen),     0, h, 36, 4);
            return h;
        }

        private static byte[] JsonBytes<T>(T obj)
            => JsonSerializer.SerializeToUtf8Bytes(obj, JsonOpts);

        private static T JsonDeserialize<T>(byte[] bytes)
            => JsonSerializer.Deserialize<T>(bytes, JsonOpts)
               ?? throw new InvalidDataException($"Failed to deserialize {typeof(T).Name}");

        private static async Task<byte[]> ReadBytesAsync(
            Stream s, int count, CancellationToken ct)
        {
            byte[] buf = new byte[count];
            await ReadExactAsync(s, buf, 0, count, ct).ConfigureAwait(false);
            return buf;
        }

        private static async Task<int> ReadExactAsync(
            Stream s, byte[] buf, int offset, int count, CancellationToken ct)
        {
            int total = 0;
            while (total < count)
            {
                int r = await s.ReadAsync(buf, offset + total, count - total, ct)
                    .ConfigureAwait(false);
                if (r == 0) break;
                total += r;
            }
            return total;
        }
    }

    /// <summary>
    /// Parsed container header with section offsets.
    /// </summary>
    public sealed class ContainerHeader
    {
        public ushort           Version           { get; init; }
        public BackupMetadata   Metadata          { get; init; } = null!;
        public BackupManifest   Manifest          { get; init; } = null!;
        public ChecksumManifest Checksums         { get; init; } = null!;
        public int              SessionKeyLength  { get; init; }
        public long             PayloadLength     { get; init; }
        public int              SignatureLength   { get; init; }
        public long             SessionKeyOffset  { get; init; }
        public long             PayloadOffset     { get; init; }
        public long             SignatureOffset   { get; init; }
        public long             TotalFileSize     { get; init; }
    }
}
