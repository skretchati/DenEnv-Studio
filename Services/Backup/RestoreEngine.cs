using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Orchestrates the full restore pipeline:
    ///
    ///   1. Validate container (full 9-step validation)
    ///   2. Derive wrapping key + decrypt session key
    ///   3. Stream-decrypt payload to intermediate MemoryStream
    ///   4. Stream-decompress + write files atomically
    ///   5. Post-restore SHA-256 verification per file
    ///   6. Log summary
    ///
    /// No System.IO.Pipelines dependency — uses MemoryStream as the
    /// decrypt→decompress buffer. Safe for payloads up to available RAM;
    /// for multi-GB archives the encrypted payload is chunked by
    /// EncryptionService so memory pressure is bounded per-chunk.
    /// </summary>
    public sealed class RestoreEngine
    {
        private static readonly Lazy<RestoreEngine> _instance =
            new(() => new RestoreEngine(), true);
        public static RestoreEngine Instance => _instance.Value;
        private RestoreEngine() { }

        public event EventHandler<RestoreProgressEventArgs>? ProgressChanged;

        // ── Entry point ───────────────────────────────────────────────

        public async Task<BackupOperationResult> RestoreBackupAsync(
            string            backupFilePath,
            string?           destinationOverride = null,
            CancellationToken ct                  = default)
        {
            var log   = LogService.Instance;
            var start = DateTime.UtcNow;

            log.Info("[Restore] ── Starting restore ───────────────────────");
            log.Info($"[Restore] Source: {Path.GetFileName(backupFilePath)}");

            // ── Step 1: Full validation ───────────────────────────────
            Report(RestorePhase.Validating, "Validating archive integrity…", 0, 0);
            log.Info("[Restore] Step 1/5 — Validating archive…");

            var validation = await ArchiveValidationService.Instance
                .ValidateAsync(backupFilePath, ct).ConfigureAwait(false);

            if (!validation.IsValid)
            {
                log.Error($"[Restore] Validation FAILED: {validation.Status} — {validation.Message}");
                foreach (string f in validation.FailedFiles)
                    log.Warn($"[Restore]   Failed: {f}");

                return BackupOperationResult.Fail(
                    start,
                    $"Restore blocked — validation failed: {validation.Message}",
                    validation.FailedFiles);
            }

            var metadata = validation.Metadata!;
            var manifest = validation.Manifest!;

            log.Info($"[Restore] Archive valid. Backup: {metadata.BackupName}");
            log.Info($"[Restore] Created: {metadata.CreatedAtLocal} on {metadata.MachineName}");
            log.Info($"[Restore] Files to restore: {manifest.FileCount}");

            // ── Step 2: Re-read header for crypto offsets ─────────────
            ContainerHeader header;
            try
            {
                header = await BackupContainerService.Instance
                    .ReadHeaderAsync(backupFilePath, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error($"[Restore] Header re-read failed: {ex.Message}");
                return BackupOperationResult.Fail(start, ex.Message);
            }

            // ── Step 3: Decrypt session key ───────────────────────────
            Report(RestorePhase.Decrypting, "Decrypting session key…", 0, 0);
            log.Info("[Restore] Step 2/5 — Decrypting session key…");

            byte[] sessionKey;
            try
            {
                byte[] wrappingKey = EncryptionService.Instance
                    .DeriveWrappingKey(metadata.BackupId);

                byte[] encSessKey = await BackupContainerService.Instance
                    .ReadEncryptedSessionKeyAsync(backupFilePath, header, ct)
                    .ConfigureAwait(false);

                // ── Remove password layer, if caching a password ──────
                if (Services.PasswordCache.Instance.HasPassword)
                {
                    log.Info("[Restore] Password protection active — removing password layer.");
                    string? password = Services.PasswordCache.Instance.GetPassword();
                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        try
                        {
                            encSessKey = Services.PasswordProtectionService.Instance
                                .UnwrapPasswordLayer(encSessKey, password);
                        }
                        catch (System.Security.Cryptography.CryptographicException ex)
                        {
                            log.Error($"[Restore] Password layer removal failed: {ex.Message}");
                            return BackupOperationResult.Fail(start,
                                "Wrong password — session key authentication failed.");
                        }
                    }
                }

                sessionKey = EncryptionService.Instance
                    .DecryptSessionKey(encSessKey, wrappingKey);

                EncryptionService.Instance.WipeKey(wrappingKey);
                log.Info("[Restore] Session key decrypted successfully.");
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                log.Error($"[Restore] Session key decryption failed: {ex.Message}");
                return BackupOperationResult.Fail(start,
                    "Session key authentication failed — archive may be corrupted " +
                    "(or a password-protected archive was opened without the correct password).");
            }

            // ── Step 4: Resolve destination ───────────────────────────
            string destRoot = destinationOverride
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            log.Info($"[Restore] Destination root: {destRoot}");

            // ── Step 5: Decrypt payload → MemoryStream → decompress ───
            Report(RestorePhase.Extracting, "Decrypting and extracting files…", 0, header.PayloadLength);
            log.Info("[Restore] Step 3/5 — Decrypting payload…");

            var state = new RestoreState();

            try
            {
                // Decrypt encrypted payload into an in-memory stream
                await using var payloadFs = BackupContainerService.Instance
                    .OpenPayloadStream(backupFilePath, header);

                var decryptedMs = new MemoryStream();
                try
                {
                    await EncryptionService.Instance.DecryptStreamAsync(
                        payloadFs,
                        decryptedMs,
                        sessionKey,
                        bytesProgress: new Progress<long>(done =>
                            Report(RestorePhase.Extracting,
                                $"Decrypting: {FormatBytes(done)} / {FormatBytes(header.PayloadLength)}",
                                done, header.PayloadLength)),
                        ct: ct).ConfigureAwait(false);
                }
                finally
                {
                    EncryptionService.Instance.WipeKey(sessionKey);
                }

                decryptedMs.Position = 0;
                log.Info($"[Restore] Payload decrypted ({FormatBytes(decryptedMs.Length)}). Extracting files…");

                Report(RestorePhase.Extracting, "Extracting files…", 0, manifest.FileCount);

                // Decompress and write files
                await CompressionService.Instance.DecompressFromStreamAsync(
                    decryptedMs,
                    destRoot,
                    header.Checksums,
                    onFile: (entry, err) => HandleFileExtracted(entry, err, state, manifest.FileCount),
                    ct: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                log.Warn("[Restore] Restore cancelled.");
                return BackupOperationResult.Cancel(start);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                log.Error($"[Restore] Decryption failed: {ex.Message}");
                return BackupOperationResult.Fail(start,
                    $"Payload decryption failed: {ex.Message}", state.Errors);
            }
            catch (Exception ex)
            {
                log.Error($"[Restore] Extraction error: {ex.Message}");
                return BackupOperationResult.Fail(start, ex.Message, state.Errors);
            }

            // ── Step 6: Post-restore file verification ────────────────
            Report(RestorePhase.Verifying,
                $"Verifying {state.RestoredPaths.Count} restored files…",
                0, state.RestoredPaths.Count);
            log.Info("[Restore] Step 4/5 — Post-restore checksum verification…");

            int verifyFailed = await VerifyRestoredFilesAsync(
                state.RestoredPaths, header.Checksums, destRoot, state.Errors, ct)
                .ConfigureAwait(false);

            // ── Step 7: Summary ───────────────────────────────────────
            var duration = DateTime.UtcNow - start;
            log.Info("[Restore] Step 5/5 — Complete.");
            log.Info($"[Restore] ✓ Restored   : {state.RestoredPaths.Count} file(s)");
            log.Info($"[Restore] ~ Errors      : {state.Errors.Count}");
            log.Info($"[Restore] ~ Verify fail : {verifyFailed}");
            log.Info($"[Restore] ✓ Duration    : {FormatDuration(duration)}");

            foreach (var err in state.Errors)
                log.Warn($"[Restore]   {err}");

            Report(RestorePhase.Done,
                $"Restore complete — {state.RestoredPaths.Count} files in {FormatDuration(duration)}",
                state.RestoredPaths.Count, state.RestoredPaths.Count);

            return new BackupOperationResult
            {
                Status         = state.Errors.Count == 0
                                    ? OperationStatus.Success
                                    : OperationStatus.PartialSuccess,
                Message        = state.Errors.Count == 0
                                    ? "Restore completed successfully."
                                    : $"Restore completed with {state.Errors.Count} error(s).",
                StartedAtUtc   = start,
                CompletedAtUtc = DateTime.UtcNow,
                FilesProcessed = state.RestoredPaths.Count,
                FilesErrored   = state.Errors.Count,
                BytesProcessed = manifest.TotalSizeBytes(),
                RestoredPaths  = state.RestoredPaths,
                Errors         = state.Errors
            };
        }

        // ── File extraction callback (no ref params) ──────────────────

        private void HandleFileExtracted(
            ManifestEntry entry, string? err,
            RestoreState  state, int totalFiles)
        {
            if (err != null)
            {
                Interlocked.Increment(ref state.FilesErrored);
                state.Errors.Add($"{entry.RelativePath}: {err}");
                LogService.Instance.Warn($"[Restore] Error: {entry.RelativePath} — {err}");
            }
            else if (entry.EntryType == "File")
            {
                int done = Interlocked.Increment(ref state.FilesDone);
                state.RestoredPaths.Add(entry.DestPath);
                LogService.Instance.Info(
                    $"[Restore] Restored: {entry.RelativePath} ({entry.SizeFmt})");
                Report(RestorePhase.Extracting,
                    $"Restoring: {entry.RelativePath}",
                    done, totalFiles);
            }
        }

        // ── Post-restore verification ─────────────────────────────────

        private static async Task<int> VerifyRestoredFilesAsync(
            List<string>      restoredPaths,
            ChecksumManifest  checksums,
            string            destRoot,
            List<string>      errors,
            CancellationToken ct)
        {
            int failed = 0;

            foreach (string absPath in restoredPaths)
            {
                ct.ThrowIfCancellationRequested();

                string rel      = GetRelativePath(destRoot, absPath);
                var    expected = checksums.FindByPath(rel);
                if (expected == null) continue;

                string? err = await ChecksumService.Instance
                    .VerifyFileAsync(absPath, expected, ct)
                    .ConfigureAwait(false);

                if (err != null)
                {
                    failed++;
                    errors.Add($"Verify failed: {rel} — {err}");
                    LogService.Instance.Warn($"[Restore] Verify failed: {rel} — {err}");
                }
                else
                {
                    LogService.Instance.Info($"[Restore] ✓ Verified: {rel}");
                }
            }

            return failed;
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void Report(RestorePhase phase, string message, long done, long total)
            => ProgressChanged?.Invoke(this,
                new RestoreProgressEventArgs(phase, message, done, total));

        private static string GetRelativePath(string root, string full)
        {
            string normRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            string normFull = Path.GetFullPath(full);
            return normFull.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase)
                ? normFull[normRoot.Length..]
                    .TrimStart(Path.DirectorySeparatorChar)
                    .Replace('\\', '/')
                : Path.GetFileName(full);
        }

        private static string FormatBytes(long b)
        {
            if (b <= 0)                   return "0 B";
            if (b < 1024)                 return $"{b} B";
            if (b < 1024 * 1024)          return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024)  return $"{b / (1024.0 * 1024):F1} MB";
            return $"{b / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static string FormatDuration(TimeSpan ts)
            => ts.TotalSeconds < 60
                ? $"{ts.TotalSeconds:F1}s"
                : $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
    }

    // ── Mutable state bag (replaces ref parameters) ───────────────────

    internal sealed class RestoreState
    {
        public List<string> RestoredPaths { get; } = new();
        public List<string> Errors        { get; } = new();
        public int          FilesDone;
        public int          FilesErrored;
    }

    // ── Progress types ────────────────────────────────────────────────

    public enum RestorePhase
    { Validating, Decrypting, Extracting, Verifying, Done }

    public sealed class RestoreProgressEventArgs : EventArgs
    {
        public RestorePhase Phase   { get; }
        public string       Message { get; }
        public long         Done    { get; }
        public long         Total   { get; }
        public double       Pct     => Total > 0 ? (double)Done / Total * 100.0 : 0;

        public RestoreProgressEventArgs(
            RestorePhase phase, string message, long done, long total)
        {
            Phase = phase; Message = message; Done = done; Total = total;
        }
    }
}
