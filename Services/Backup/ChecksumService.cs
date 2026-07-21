using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// SHA-256 streaming checksums for individual files and HMAC-SHA256
    /// container signatures. Stateless — all methods are pure/static-friendly.
    /// </summary>
    public sealed class ChecksumService
    {
        private static readonly Lazy<ChecksumService> _instance =
            new(() => new ChecksumService(), true);
        public static ChecksumService Instance => _instance.Value;
        private ChecksumService() { }

        // ── File checksum ────────────────────────────────────────────

        /// <summary>
        /// Computes SHA-256 of a file via streaming (no full load into memory).
        /// Returns lowercase hex string.
        /// </summary>
        public async Task<string> ComputeFileHashAsync(
            string filePath, CancellationToken ct = default)
        {
            using var sha  = SHA256.Create();
            using var stream = new FileStream(
                filePath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, 81920, useAsync: true);

            byte[] hash = await ComputeHashAsync(sha, stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Computes SHA-256 of a stream segment (used for post-restore verification).
        /// Caller must position stream before calling.
        /// </summary>
        public async Task<string> ComputeStreamHashAsync(
            Stream stream, CancellationToken ct = default)
        {
            using var sha = SHA256.Create();
            byte[] hash = await ComputeHashAsync(sha, stream, ct).ConfigureAwait(false);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Builds ChecksumEntry for a single file.
        /// </summary>
        public async Task<ChecksumEntry> BuildEntryAsync(
            string relativePath, string absolutePath, CancellationToken ct = default)
        {
            var fi   = new FileInfo(absolutePath);
            string hex = await ComputeFileHashAsync(absolutePath, ct).ConfigureAwait(false);

            return new ChecksumEntry
            {
                RelativePath = relativePath,
                Sha256Hex    = hex,
                SizeBytes    = fi.Length
            };
        }

        // ── Manifest build ───────────────────────────────────────────

        /// <summary>
        /// Builds the full ChecksumManifest for a set of (relativePath, absolutePath) pairs.
        /// Runs hashing in parallel with a degree cap to avoid I/O saturation.
        /// </summary>
        public async Task<ChecksumManifest> BuildManifestAsync(
            string backupId,
            IReadOnlyList<(string relative, string absolute)> files,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default)
        {
            var entries = new ChecksumEntry[files.Count];
            int done    = 0;

            // Parallelism cap: 4 concurrent SHA computations
            var sem = new SemaphoreSlim(4, 4);
            var tasks = new Task[files.Count];

            for (int i = 0; i < files.Count; i++)
            {
                int idx       = i;
                var (rel, abs) = files[idx];

                tasks[idx] = Task.Run(async () =>
                {
                    await sem.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        entries[idx] = await BuildEntryAsync(rel, abs, ct)
                            .ConfigureAwait(false);
                        int d = Interlocked.Increment(ref done);
                        progress?.Report((d, files.Count));
                    }
                    finally { sem.Release(); }
                }, ct);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new ChecksumManifest
            {
                BackupId = backupId,
                Entries  = new List<ChecksumEntry>(entries)
            };
        }

        // ── Verification ─────────────────────────────────────────────

        /// <summary>
        /// Verifies a restored file against its expected checksum.
        /// Returns null on success, error description on failure.
        /// </summary>
        public async Task<string?> VerifyFileAsync(
            string absolutePath, ChecksumEntry expected,
            CancellationToken ct = default)
        {
            try
            {
                var fi = new FileInfo(absolutePath);
                if (!fi.Exists)
                    return $"File not found after restore: {absolutePath}";

                if (fi.Length != expected.SizeBytes)
                    return $"Size mismatch: expected {expected.SizeBytes}, got {fi.Length}";

                string actual = await ComputeFileHashAsync(absolutePath, ct)
                    .ConfigureAwait(false);

                if (!string.Equals(actual, expected.Sha256Hex, StringComparison.OrdinalIgnoreCase))
                    return $"SHA-256 mismatch: expected {expected.Sha256Hex}, got {actual}";

                return null; // success
            }
            catch (Exception ex)
            {
                return $"Verification error: {ex.Message}";
            }
        }

        // ── HMAC-SHA256 container signature ──────────────────────────

        /// <summary>
        /// Computes HMAC-SHA256 over all bytes written to the container
        /// BEFORE the signature section. Key is the wrapping key derived
        /// by EncryptionService — caller provides it.
        /// Returns 32-byte signature.
        /// </summary>
        public byte[] ComputeContainerSignature(byte[] wrappingKey, byte[] containerBytes)
        {
            using var hmac = new HMACSHA256(wrappingKey);
            return hmac.ComputeHash(containerBytes);
        }

        /// <summary>
        /// Stream variant — avoids double-buffering the entire container.
        /// Stream must be positioned at offset 0.
        /// </summary>
        public byte[] ComputeContainerSignatureFromStream(byte[] wrappingKey, Stream stream)
        {
            long pos = stream.CanSeek ? stream.Position : 0;
            using var hmac = new HMACSHA256(wrappingKey);
            byte[] sig = hmac.ComputeHash(stream);
            if (stream.CanSeek) stream.Position = pos;
            return sig;
        }

        /// <summary>
        /// Constant-time comparison to prevent timing attacks.
        /// </summary>
        public bool VerifySignature(byte[] expected, byte[] actual)
        {
            if (expected.Length != actual.Length) return false;
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        // ── Internal ─────────────────────────────────────────────────

        private static async Task<byte[]> ComputeHashAsync(
            HashAlgorithm sha, Stream stream, CancellationToken ct)
        {
            const int bufSize = 81920; // 80 KB
            byte[] buf = new byte[bufSize];
            int read;

            while ((read = await stream.ReadAsync(buf, 0, bufSize, ct)
                .ConfigureAwait(false)) > 0)
            {
                sha.TransformBlock(buf, 0, read, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha.Hash!;
        }
    }
}
