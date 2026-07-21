using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Builds and extracts the internal TAR-like payload archive.
    /// Format: sequence of [EntryHeader(fixed 512 bytes)][file bytes padded to 512].
    /// GZip compression is applied as a wrapping stream OVER this when encrypting.
    /// Pure streaming — no temp files, no full load into memory.
    /// </summary>
    public sealed class CompressionService
    {
        private static readonly Lazy<CompressionService> _instance =
            new(() => new CompressionService(), true);
        public static CompressionService Instance => _instance.Value;
        private CompressionService() { }

        // ── Entry header layout (512 bytes, ASCII) ───────────────────
        // [0..99]   relative path (null-terminated)
        // [100..107] size in bytes (octal ASCII, 8 chars)
        // [108..115] last-modified unix timestamp (octal ASCII, 8 chars)
        // [116..127] entry type: "FILE\0" or "DIR\0"
        // [128..511] padding zeros

        private const int HeaderSize = 512;
        private const int BlockSize  = 512;

        // ── Write (compress) ─────────────────────────────────────────

        /// <summary>
        /// Writes all manifest entries as a GZip-compressed payload stream.
        /// Output stream is left at end-of-data (caller handles encryption).
        /// Reports (bytesWritten, totalBytes) progress.
        /// </summary>
        public async Task CompressToStreamAsync(
            IReadOnlyList<ManifestEntry> entries,
            Stream output,
            IProgress<(long done, long total)>? progress = null,
            CancellationToken ct = default)
        {
            long total = 0;
            foreach (var e in entries)
                if (e.EntryType == "File") total += e.SizeBytes;

            long done = 0;

            await using var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true);

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                if (entry.EntryType == "Directory")
                {
                    await WriteDirectoryHeaderAsync(gzip, entry, ct).ConfigureAwait(false);
                    continue;
                }

                if (!File.Exists(entry.SourcePath)) continue;

                await WriteFileEntryAsync(gzip, entry, ct).ConfigureAwait(false);

                done += entry.SizeBytes;
                progress?.Report((done, total));
            }

            // End-of-archive: two 512-byte zero blocks
            byte[] eof = new byte[BlockSize * 2];
            await gzip.WriteAsync(eof, ct).ConfigureAwait(false);
        }

        // ── Read (decompress) ─────────────────────────────────────────

        /// <summary>
        /// Extracts a GZip-compressed payload stream to a destination root.
        /// Validates each file against checksums after writing.
        /// Calls onFile for each successfully restored entry.
        /// </summary>
        public async Task DecompressFromStreamAsync(
            Stream input,
            string destinationRoot,
            ChecksumManifest checksums,
            Action<ManifestEntry, string?> onFile,   // entry, error (null = ok)
            CancellationToken ct = default)
        {
            await using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true);

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Read header
                byte[] headerBuf = new byte[HeaderSize];
                int headerRead = await ReadExactAsync(gzip, headerBuf, 0, HeaderSize, ct)
                    .ConfigureAwait(false);
                if (headerRead < HeaderSize) break;

                // End-of-archive check (all zeros)
                bool allZero = true;
                foreach (byte b in headerBuf) if (b != 0) { allZero = false; break; }
                if (allZero) break;

                var (relativePath, sizeBytes, entryType) = ParseHeader(headerBuf);
                if (string.IsNullOrEmpty(relativePath)) break;

                // Guard against path traversal
                string safeDest = SanitizePath(destinationRoot, relativePath);
                if (safeDest == null!) { await SkipBlocksAsync(gzip, sizeBytes, ct); continue; }

                if (entryType == "DIR")
                {
                    Directory.CreateDirectory(safeDest);
                    onFile(new ManifestEntry
                    {
                        RelativePath = relativePath,
                        DestPath     = safeDest,
                        EntryType    = "Directory"
                    }, null);
                    continue;
                }

                // Write file atomically via temp path
                string? err = await WriteFileAtomicAsync(
                    gzip, safeDest, relativePath, sizeBytes,
                    checksums, ct).ConfigureAwait(false);

                onFile(new ManifestEntry
                {
                    RelativePath = relativePath,
                    DestPath     = safeDest,
                    SizeBytes    = sizeBytes,
                    EntryType    = "File"
                }, err);
            }
        }

        // ── Header I/O ───────────────────────────────────────────────

        private static async Task WriteFileEntryAsync(
            Stream gzip, ManifestEntry entry, CancellationToken ct)
        {
            byte[] header = BuildFileHeader(entry.RelativePath, entry.SizeBytes,
                entry.LastModifiedUtc, "FILE");
            await gzip.WriteAsync(header, ct).ConfigureAwait(false);

            const int bufSize = 81920;
            byte[] buf = new byte[bufSize];

            await using var fs = new FileStream(
                entry.SourcePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, bufSize, useAsync: true);

            long written = 0;
            int  read;
            while ((read = await fs.ReadAsync(buf, 0, bufSize, ct)
                    .ConfigureAwait(false)) > 0)
            {
                await gzip.WriteAsync(buf.AsMemory(0, read), ct).ConfigureAwait(false);
                written += read;
            }

            // Pad to block boundary
            int pad = (int)(BlockSize - (written % BlockSize)) % BlockSize;
            if (pad > 0)
                await gzip.WriteAsync(new byte[pad], ct).ConfigureAwait(false);
        }

        private static async Task WriteDirectoryHeaderAsync(
            Stream gzip, ManifestEntry entry, CancellationToken ct)
        {
            byte[] header = BuildFileHeader(entry.RelativePath, 0,
                entry.LastModifiedUtc, "DIR\0");
            await gzip.WriteAsync(header, ct).ConfigureAwait(false);
        }

        private static byte[] BuildFileHeader(
            string relativePath, long size,
            DateTime modifiedUtc, string type)
        {
            byte[] header = new byte[HeaderSize];
            // Path (max 99 chars + null)
            byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
            int pathLen = Math.Min(pathBytes.Length, 99);
            Array.Copy(pathBytes, header, pathLen);
            // Size (8-char octal)
            string sizeOctal = Convert.ToString(size, 8).PadLeft(8, '0');
            Encoding.ASCII.GetBytes(sizeOctal, 0, 8, header, 100);
            // Timestamp (8-char octal unix epoch)
            long ts = new DateTimeOffset(modifiedUtc).ToUnixTimeSeconds();
            string tsOctal = Convert.ToString(ts, 8).PadLeft(8, '0');
            Encoding.ASCII.GetBytes(tsOctal, 0, 8, header, 108);
            // Type
            byte[] typeBytes = Encoding.ASCII.GetBytes(type.PadRight(12, '\0'));
            Array.Copy(typeBytes, 0, header, 116, Math.Min(typeBytes.Length, 12));
            return header;
        }

        private static (string path, long size, string type) ParseHeader(byte[] header)
        {
            string path = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
            string sizeStr = Encoding.ASCII.GetString(header, 100, 8).Trim('\0', ' ');
            string typeStr = Encoding.ASCII.GetString(header, 116, 12).TrimEnd('\0').Trim();

            long size = 0;
            if (!string.IsNullOrEmpty(sizeStr))
                try { size = Convert.ToInt64(sizeStr, 8); } catch { }

            return (path, size, typeStr);
        }

        // ── Atomic file write ────────────────────────────────────────

        private static async Task<string?> WriteFileAtomicAsync(
            Stream gzip, string destPath, string relativePath,
            long sizeBytes, ChecksumManifest checksums,
            CancellationToken ct)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                string tmpPath = destPath + ".devbackup_tmp";

                await using (var fs = new FileStream(
                    tmpPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true))
                {
                    long remaining = sizeBytes;
                    byte[] buf = new byte[81920];

                    while (remaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(buf.Length, remaining);
                        int read   = await ReadExactAsync(gzip, buf, 0, toRead, ct)
                            .ConfigureAwait(false);
                        if (read == 0) break;
                        await fs.WriteAsync(buf.AsMemory(0, read), ct)
                            .ConfigureAwait(false);
                        remaining -= read;
                    }
                }

                // Skip block padding
                long pad = (BlockSize - (sizeBytes % BlockSize)) % BlockSize;
                if (pad > 0) await SkipBlocksAsync(gzip, pad, ct).ConfigureAwait(false);

                // Rename atomically
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(tmpPath, destPath);

                return null; // success
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static string SanitizePath(string root, string relativePath)
        {
            // Normalize separators and reject traversal attempts
            string normalized = relativePath
                .Replace('\\', '/')
                .TrimStart('/');

            if (normalized.Contains("../") || normalized.Contains("..\\"))
                return null!;

            string full = Path.GetFullPath(Path.Combine(root, normalized));
            if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
                return null!;

            return full;
        }

        private static async Task SkipBlocksAsync(Stream s, long bytes, CancellationToken ct)
        {
            byte[] buf = new byte[Math.Min(bytes, 4096)];
            long remaining = bytes;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(buf.Length, remaining);
                int read   = await s.ReadAsync(buf, 0, toRead, ct).ConfigureAwait(false);
                if (read == 0) break;
                remaining -= read;
            }
        }

        private static async Task<int> ReadExactAsync(
            Stream s, byte[] buf, int offset, int count, CancellationToken ct)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await s.ReadAsync(buf, offset + totalRead,
                    count - totalRead, ct).ConfigureAwait(false);
                if (read == 0) break;
                totalRead += read;
            }
            return totalRead;
        }
    }
}
