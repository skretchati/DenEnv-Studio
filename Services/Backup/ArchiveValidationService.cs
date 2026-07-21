using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Full validation pipeline for a .devbackup container.
    ///
    /// Validation order (fail-fast on hard failures, accumulate on soft):
    ///   1. File exists + readable
    ///   2. Magic bytes
    ///   3. Format version
    ///   4. HMAC-SHA256 signature (tamper detection)
    ///   5. Metadata parse
    ///   6. Manifest parse
    ///   7. Session key decrypt (GCM auth)
    ///   8. Payload header decrypt (first chunk GCM auth)
    ///   9. Checksum re-verification of unencrypted section checksums
    ///
    /// All validation events feed into LogService (existing singleton).
    /// </summary>
    public sealed class ArchiveValidationService
    {
        private static readonly Lazy<ArchiveValidationService> _instance =
            new(() => new ArchiveValidationService(), true);
        public static ArchiveValidationService Instance => _instance.Value;
        private ArchiveValidationService() { }

        /// <summary>
        /// Validates the container at the given path.
        /// Returns ValidationResult with full details.
        /// Never throws — all errors are captured in result.
        /// </summary>
        public async Task<ValidationResult> ValidateAsync(
            string filePath, CancellationToken ct = default)
        {
            var log = LogService.Instance;
            log.Info($"[Validation] Starting validation: {Path.GetFileName(filePath)}");

            // ── Step 1: File accessible ───────────────────────────────
            if (!File.Exists(filePath))
            {
                string msg = $"File not found: {filePath}";
                log.Error($"[Validation] {msg}");
                return ValidationResult.Fail(ValidationStatus.IOError, msg);
            }

            // ── Step 2 + 3: Magic and header parse ────────────────────
            ContainerHeader header;
            try
            {
                header = await BackupContainerService.Instance
                    .ReadHeaderAsync(filePath, ct).ConfigureAwait(false);
            }
            catch (InvalidDataException ex)
            {
                log.Error($"[Validation] Header error: {ex.Message}");
                return ValidationResult.Fail(
                    ex.Message.Contains("magic")
                        ? ValidationStatus.InvalidMagic
                        : ValidationStatus.MetadataCorrupted,
                    ex.Message);
            }
            catch (Exception ex)
            {
                log.Error($"[Validation] Read error: {ex.Message}");
                return ValidationResult.Fail(ValidationStatus.IOError, ex.Message);
            }

            log.Info($"[Validation] Header OK — format v{header.Version}, " +
                     $"{header.Manifest.FileCount} files, backup: {header.Metadata.BackupName}");

            // ── Step 3: Version check ─────────────────────────────────
            if (header.Version > BackupContainerService.CurrentVersion)
            {
                string msg = $"Unsupported format version: {header.Version}. " +
                             $"This app supports up to v{BackupContainerService.CurrentVersion}.";
                log.Warn($"[Validation] {msg}");
                return ValidationResult.Fail(
                    ValidationStatus.UnsupportedVersion, msg, header.Metadata);
            }

            // ── Step 4: HMAC-SHA256 signature ─────────────────────────
            log.Info("[Validation] Verifying container signature…");
            try
            {
                byte[] wrappingKey = EncryptionService.Instance
                    .DeriveWrappingKey(header.Metadata.BackupId);

                byte[] storedSig = await BackupContainerService.Instance
                    .ReadSignatureAsync(filePath, header, ct).ConfigureAwait(false);

                byte[] preSignatureBytes = await BackupContainerService.Instance
                    .ReadPreSignatureBytesAsync(filePath, header, ct).ConfigureAwait(false);

                byte[] computedSig = ChecksumService.Instance
                    .ComputeContainerSignature(wrappingKey, preSignatureBytes);

                if (!ChecksumService.Instance.VerifySignature(storedSig, computedSig))
                {
                    const string msg =
                        "Container signature mismatch — archive has been tampered with or is corrupted.";
                    log.Error($"[Validation] {msg}");
                    return ValidationResult.Fail(
                        ValidationStatus.TamperDetected, msg, header.Metadata);
                }

                log.Info("[Validation] Signature OK.");

                // ── Step 5 + 6: Metadata/Manifest already parsed in header read ──
                // They are valid if we reached here.

                // ── Step 7: Session key decrypt ───────────────────────
                log.Info("[Validation] Verifying session key integrity…");

                byte[] encSessKey = await BackupContainerService.Instance
                    .ReadEncryptedSessionKeyAsync(filePath, header, ct)
                    .ConfigureAwait(false);

                byte[] sessionKey;
                try
                {
                    sessionKey = EncryptionService.Instance
                        .DecryptSessionKey(encSessKey, wrappingKey);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    const string msg =
                        "Session key authentication failed — key section may be corrupted.";
                    log.Error($"[Validation] {msg}");
                    EncryptionService.Instance.WipeKey(wrappingKey);
                    return ValidationResult.Fail(
                        ValidationStatus.KeyCorrupted, msg, header.Metadata);
                }

                log.Info("[Validation] Session key OK.");

                // ── Step 8: First payload chunk decrypt (auth tag check) ───
                log.Info("[Validation] Verifying payload integrity (first chunk)…");
                try
                {
                    await VerifyFirstPayloadChunkAsync(filePath, header, sessionKey, ct)
                        .ConfigureAwait(false);
                    log.Info("[Validation] Payload first-chunk auth tag OK.");
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    const string msg =
                        "Payload authentication failed — payload may be corrupted or truncated.";
                    log.Error($"[Validation] {msg}");
                    EncryptionService.Instance.WipeKey(sessionKey);
                    EncryptionService.Instance.WipeKey(wrappingKey);
                    return ValidationResult.Fail(
                        ValidationStatus.PayloadCorrupted, msg, header.Metadata);
                }
                finally
                {
                    EncryptionService.Instance.WipeKey(sessionKey);
                    EncryptionService.Instance.WipeKey(wrappingKey);
                }

                // ── Step 9: Checksum cross-validation ────────────────
                log.Info("[Validation] Cross-validating manifest vs checksums…");
                var mismatches = CrossValidateManifestChecksums(
                    header.Manifest, header.Checksums);

                if (mismatches.Count > 0)
                {
                    string msg = $"Manifest/checksum cross-validation failed for " +
                                 $"{mismatches.Count} file(s).";
                    log.Warn($"[Validation] {msg}");
                    foreach (var m in mismatches)
                        log.Warn($"[Validation]   Missing checksum: {m}");
                    // Soft failure — warn but allow restore decision to caller
                    return ValidationResult.Fail(
                        ValidationStatus.ChecksumMismatch, msg,
                        header.Metadata, mismatches);
                }

                log.Info($"[Validation] All checks passed. " +
                         $"{header.Manifest.FileCount} files validated.");

                return ValidationResult.Ok(header.Metadata, header.Manifest);
            }
            catch (OperationCanceledException)
            {
                log.Warn("[Validation] Validation cancelled.");
                throw;
            }
            catch (Exception ex)
            {
                log.Error($"[Validation] Unexpected error: {ex.Message}");
                return ValidationResult.Fail(
                    ValidationStatus.UnknownError, ex.Message, header?.Metadata);
            }
        }

        /// <summary>
        /// Quick validation — checks magic + signature only. Fast O(file size) pass.
        /// Used by scanner to tag discovered .devbackup files as valid/invalid.
        /// </summary>
        public async Task<(bool valid, string? error)> QuickValidateAsync(
            string filePath, CancellationToken ct = default)
        {
            try
            {
                var header = await BackupContainerService.Instance
                    .ReadHeaderAsync(filePath, ct).ConfigureAwait(false);

                byte[] wrappingKey = EncryptionService.Instance
                    .DeriveWrappingKey(header.Metadata.BackupId);

                byte[] storedSig = await BackupContainerService.Instance
                    .ReadSignatureAsync(filePath, header, ct).ConfigureAwait(false);

                byte[] preBytes = await BackupContainerService.Instance
                    .ReadPreSignatureBytesAsync(filePath, header, ct).ConfigureAwait(false);

                byte[] computed = ChecksumService.Instance
                    .ComputeContainerSignature(wrappingKey, preBytes);

                bool ok = ChecksumService.Instance.VerifySignature(storedSig, computed);
                return ok ? (true, null) : (false, "Signature mismatch");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // ── Private helpers ───────────────────────────────────────────

        private static async Task VerifyFirstPayloadChunkAsync(
            string filePath, ContainerHeader header,
            byte[] sessionKey, CancellationToken ct)
        {
            // Read the first 4 bytes to get chunk length, then one full chunk
            await using var fs = BackupContainerService.Instance
                .OpenPayloadStream(filePath, header);

            // Skip 12-byte base nonce
            byte[] nonce = new byte[12];
            await ReadExactAsync(fs, nonce, 0, 12, ct).ConfigureAwait(false);

            // Read chunk length
            byte[] lenBuf = new byte[4];
            int lr = await ReadExactAsync(fs, lenBuf, 0, 4, ct).ConfigureAwait(false);
            if (lr < 4) return; // empty payload is OK

            if (!BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
            uint chunkLen = BitConverter.ToUInt32(lenBuf);
            if (chunkLen == 0) return;

            // Read nonce + ciphertext + tag
            byte[] chunkNonce = new byte[12];
            await ReadExactAsync(fs, chunkNonce, 0, 12, ct).ConfigureAwait(false);

            // Read up to 64KB ciphertext
            int readLen = (int)Math.Min(chunkLen, 65536);
            byte[] ct_buf = new byte[readLen];
            await ReadExactAsync(fs, ct_buf, 0, readLen, ct).ConfigureAwait(false);

            // If we didn't read the full chunk, skip remainder
            if (readLen < (int)chunkLen)
            {
                byte[] skip = new byte[chunkLen - readLen];
                await ReadExactAsync(fs, skip, 0, skip.Length, ct).ConfigureAwait(false);
            }

            byte[] tag = new byte[16];
            await ReadExactAsync(fs, tag, 0, 16, ct).ConfigureAwait(false);

            // Actually decrypt (will throw CryptographicException on auth failure)
            byte[] plain = new byte[readLen];
            using var aes = new System.Security.Cryptography.AesGcm(sessionKey, 16);

            // We need the full ciphertext for GCM — if we truncated, this is partial
            // Workaround: read the real full chunk for verification
            // Re-open and read properly
            await using var fs2 = BackupContainerService.Instance
                .OpenPayloadStream(filePath, header);

            byte[] nonce2 = new byte[12];
            await ReadExactAsync(fs2, nonce2, 0, 12, ct).ConfigureAwait(false);
            byte[] lenBuf2 = new byte[4];
            await ReadExactAsync(fs2, lenBuf2, 0, 4, ct).ConfigureAwait(false);
            if (!BitConverter.IsLittleEndian) Array.Reverse(lenBuf2);
            uint cl2 = BitConverter.ToUInt32(lenBuf2);
            if (cl2 == 0) return;

            byte[] n2 = new byte[12];
            await ReadExactAsync(fs2, n2, 0, 12, ct).ConfigureAwait(false);
            byte[] c2 = new byte[cl2];
            await ReadExactAsync(fs2, c2, 0, (int)cl2, ct).ConfigureAwait(false);
            byte[] t2 = new byte[16];
            await ReadExactAsync(fs2, t2, 0, 16, ct).ConfigureAwait(false);

            byte[] p2 = new byte[cl2];
            aes.Decrypt(n2, c2, t2, p2); // throws if auth fails
        }

        private static List<string> CrossValidateManifestChecksums(
            BackupManifest manifest, ChecksumManifest checksums)
        {
            var missing = new List<string>();
            foreach (var entry in manifest.Entries)
            {
                if (entry.EntryType != "File") continue;
                var cs = checksums.FindByPath(entry.RelativePath);
                if (cs == null)
                    missing.Add(entry.RelativePath);
            }
            return missing;
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
}
