using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DevEnvStudio.Services.Backup.Models;
using System.Linq;

namespace DevEnvStudio.Services.Backup
{
    /// <summary>
    /// Orchestrates the full backup creation pipeline:
    ///
    ///   1. Collect files via ManifestBuilder
    ///   2. Compute SHA-256 checksums via ChecksumService
    ///   3. Build metadata snapshot via MetadataBuilder
    ///   4. Generate AES-256 session key
    ///   5. Compress payload (GZip streaming)
    ///   6. Encrypt payload (AES-256-GCM chunked)
    ///   7. Encrypt session key (HKDF-derived wrapping key)
    ///   8. Write .devbackup container
    ///   9. Sign container (HMAC-SHA256)
    ///  10. Verify output integrity
    ///
    /// All progress and log events route through LogService (existing singleton).
    /// Never blocks the calling thread — fully async throughout.
    /// </summary>
    public sealed class BackupEngine
    {
        private static readonly Lazy<BackupEngine> _instance =
            new(() => new BackupEngine(), true);
        public static BackupEngine Instance => _instance.Value;
        private BackupEngine() { }

        // ── Progress event ────────────────────────────────────────────
        /// <summary>
        /// Raised on the background thread; callers must marshal to UI if needed.
        /// </summary>
        public event EventHandler<BackupProgressEventArgs>? ProgressChanged;

        // ── Entry point ───────────────────────────────────────────────

        /// <summary>
        /// Creates a .devbackup archive from the given profile.
        /// Returns a BackupOperationResult — never throws (errors captured in result).
        /// </summary>
        public async Task<BackupOperationResult> CreateBackupAsync(
            BackupProfile     profile,
            CancellationToken ct = default)
        {
            var log   = LogService.Instance;
            var start = DateTime.UtcNow;

            string backupId = Guid.NewGuid().ToString();
            string fileName = MetadataBuilder.BuildFileName(profile.Name);

            // If password protection is active AND the password is cached,
            // mark the filename so the UI can identify protected backups.
            // The password must actually be in memory — a setting alone is not enough,
            // otherwise the backup would claim protection without real double-wrapping.
            bool passwordEnabled = Services.SettingsService.Instance.PasswordProtectionEnabled;
            string? cachedPwd    = Services.PasswordCache.Instance.GetPassword();
            bool actuallyProtected = passwordEnabled && !string.IsNullOrWhiteSpace(cachedPwd);

            if (actuallyProtected)
            {
                string baseName = Path.GetFileNameWithoutExtension(fileName);
                fileName = $"{baseName}[PROTECTED]{Path.GetExtension(fileName)}";
            }
            else if (passwordEnabled)
            {
                log.Warn("[Backup] Password protection is enabled in settings but no " +
                         "password is cached — backup will NOT be double-wrapped.");
            }

            string outputDir = profile.ResolvedDestination;
            string tmpPath  = Path.Combine(outputDir, fileName + ".tmp");
            string finalPath = Path.Combine(outputDir, fileName);

            log.Info($"[Backup] ── Starting backup ──────────────────────────");
            log.Info($"[Backup] Profile  : {profile.Name}");
            log.Info($"[Backup] Output   : {finalPath}");
            log.Info($"[Backup] BackupId : {backupId}");

            try
            {
                Directory.CreateDirectory(outputDir);

                // ── Phase 1: File collection ─────────────────────────
                Report(BackupPhase.Collecting, "Collecting files…", 0, 0);
                log.Info("[Backup] Phase 1/6 — Collecting files…");

                int filesSkipped = 0;
                var manifest = await ManifestBuilder.Instance.BuildAsync(
                    backupId, profile,
                    onFileDiscovered: f => log.Info($"[Backup] + {f}"),
                    onFileSkipped:    f =>
                    {
                        Interlocked.Increment(ref filesSkipped);
                        log.Info($"[Backup] ~ Skipped: {f}");
                    },
                    ct: ct).ConfigureAwait(false);

                log.Info($"[Backup] Collected {manifest.FileCount} file(s), " +
                         $"{filesSkipped} skipped.");

                if (ct.IsCancellationRequested)
                    return BackupOperationResult.Cancel(start);

                // ── Phase 2: Checksums ────────────────────────────────
                Report(BackupPhase.Checksums, $"Computing checksums for {manifest.FileCount} files…", 0, manifest.FileCount);
                log.Info("[Backup] Phase 2/6 — Computing SHA-256 checksums…");

                int checksumDone = 0;
                var filePairs = manifest.GetFilePairs();
                var checksums = await ChecksumService.Instance.BuildManifestAsync(
                    backupId, filePairs,
                    progress: new Progress<(int done, int total)>(p =>
                    {
                        checksumDone = p.done;
                        Report(BackupPhase.Checksums,
                            $"Checksums: {p.done}/{p.total}",
                            p.done, p.total);
                    }),
                    ct: ct).ConfigureAwait(false);

                log.Info($"[Backup] Checksums computed for {checksums.Entries.Count} file(s).");

                if (ct.IsCancellationRequested)
                    return BackupOperationResult.Cancel(start);

                // ── Phase 3: Session key + wrapping key ───────────────
                log.Info("[Backup] Phase 3/6 — Generating encryption keys…");

                byte[] sessionKey  = EncryptionService.Instance.GenerateSessionKey();
                byte[] wrappingKey = EncryptionService.Instance.DeriveWrappingKey(backupId);
                byte[] encSessKey  = EncryptionService.Instance.EncryptSessionKey(
                    sessionKey, wrappingKey);

                // ── Double-wrap with user password if cached ───────────
                if (actuallyProtected)
                {
                    log.Info("[Backup] Password protection active — double-wrapping session key.");
                    encSessKey = Services.PasswordProtectionService.Instance
                        .DoubleWrapKey(encSessKey, cachedPwd!);
                }

                log.Info("[Backup] Session key generated and wrapped.");

                // ── Phase 4: Compress + Encrypt payload to temp stream ─
                Report(BackupPhase.Compressing, "Compressing and encrypting payload…", 0, manifest.TotalSizeBytes());
                log.Info("[Backup] Phase 4/6 — Compressing and encrypting payload…");

                long payloadSize;
                using var payloadMs = new MemoryStream(); // for small-medium payloads

                // For very large payloads (> 512 MB) we'd use a temp file instead.
                // Threshold check:
                bool useTempFile = manifest.TotalSizeBytes() > 512L * 1024 * 1024;
                Stream payloadStream = useTempFile
                    ? (Stream)new FileStream(tmpPath, FileMode.Create, FileAccess.ReadWrite,
                        FileShare.None, 81920, useAsync: true)
                    : payloadMs;

                try
                {
                    // Inner pipeline: GZip → AES-GCM chunks → payloadStream
                    // We compress to an intermediate stream first, then encrypt
                    using var compressedMs = new MemoryStream();

                    await CompressionService.Instance.CompressToStreamAsync(
                        manifest.Entries,
                        compressedMs,
                        progress: new Progress<(long done, long total)>(p =>
                            Report(BackupPhase.Compressing,
                                $"Compressing: {FormatBytes(p.done)} / {FormatBytes(p.total)}",
                                p.done, p.total)),
                        ct: ct).ConfigureAwait(false);

                    compressedMs.Position = 0;
                    long compressedSize = compressedMs.Length;
                    log.Info($"[Backup] Compressed: {FormatBytes(manifest.TotalSizeBytes())} → " +
                             $"{FormatBytes(compressedSize)} " +
                             $"({100.0 - compressedSize * 100.0 / Math.Max(manifest.TotalSizeBytes(), 1):F1}% reduction)");

                    Report(BackupPhase.Encrypting, "Encrypting payload…", 0, compressedSize);
                    log.Info("[Backup] Phase 5/6 — Encrypting payload…");

                    await EncryptionService.Instance.EncryptStreamAsync(
                        compressedMs, payloadStream, sessionKey,
                        bytesProgress: new Progress<long>(done =>
                            Report(BackupPhase.Encrypting,
                                $"Encrypting: {FormatBytes(done)} / {FormatBytes(compressedSize)}",
                                done, compressedSize)),
                        ct: ct).ConfigureAwait(false);

                    payloadSize = payloadStream.Position;
                    payloadStream.Position = 0;

                    log.Info($"[Backup] Encrypted payload: {FormatBytes(payloadSize)}");
                }
                finally
                {
                    // Wipe session key from memory immediately after encryption
                    EncryptionService.Instance.WipeKey(sessionKey);
                }

                if (ct.IsCancellationRequested)
                {
                    CleanupTemp(tmpPath);
                    return BackupOperationResult.Cancel(start);
                }

                // ── Phase 5: Build metadata (with final sizes) ─────────
                var systemInfo = await SystemInfoService.Instance
                    .GetSnapshotAsync().ConfigureAwait(false);

                var metadata = MetadataBuilder.Instance.Build(
                    backupId, profile, manifest,
                    osVersion:      systemInfo.OsVersion,
                    compressedSize: payloadSize,
                    isProtected:    actuallyProtected);

                // ── Phase 6: Write container + sign ───────────────────
                Report(BackupPhase.Writing, "Writing .devbackup container…", 0, 0);
                log.Info("[Backup] Phase 6/6 — Writing container and signing…");

                await BackupContainerService.Instance.WriteContainerAsync(
                    finalPath,
                    metadata,
                    manifest,
                    checksums,
                    encSessKey,
                    payloadStream,
                    wrappingKey,
                    ct).ConfigureAwait(false);

                // Wipe wrapping key
                EncryptionService.Instance.WipeKey(wrappingKey);

                if (useTempFile)
                {
                    await payloadStream.DisposeAsync().ConfigureAwait(false);
                    if (File.Exists(tmpPath)) File.Delete(tmpPath);
                }

                var fi = new FileInfo(finalPath);
                log.Info($"[Backup] Container written: {fi.Name} ({FormatBytes(fi.Length)})");

                // ── Post-write: quick signature validation ────────────
                Report(BackupPhase.Verifying, "Verifying output…", 0, 0);
                log.Info("[Backup] Verifying output signature…");

                var (quickOk, quickErr) = await ArchiveValidationService.Instance
                    .QuickValidateAsync(finalPath, ct).ConfigureAwait(false);

                if (!quickOk)
                {
                    log.Error($"[Backup] Output verification FAILED: {quickErr}");
                    return BackupOperationResult.Fail(
                        start, $"Backup written but verification failed: {quickErr}");
                }

                var duration = DateTime.UtcNow - start;
                log.Info($"[Backup] ✓ Complete in {FormatDuration(duration)} — {fileName}");
                Report(BackupPhase.Done, $"Backup complete: {fileName}", manifest.FileCount, manifest.FileCount);

                return BackupOperationResult.Succeed(
                    start,
                    files:      manifest.FileCount,
                    bytes:      manifest.TotalSizeBytes(),
                    outputPath: finalPath,
                    skipped:    filesSkipped);
            }
            catch (OperationCanceledException)
            {
                CleanupTemp(tmpPath);
                CleanupTemp(finalPath);
                log.Warn("[Backup] Backup cancelled.");
                return BackupOperationResult.Cancel(start);
            }
            catch (Exception ex)
            {
                CleanupTemp(tmpPath);
                CleanupTemp(finalPath);
                log.Error($"[Backup] Fatal error: {ex.Message}");
                return BackupOperationResult.Fail(start, ex.Message);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private void Report(BackupPhase phase, string message, long done, long total)
            => ProgressChanged?.Invoke(this, new BackupProgressEventArgs(phase, message, done, total));

        private static void CleanupTemp(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static string FormatBytes(long b)
        {
            if (b < 1024)             return $"{b} B";
            if (b < 1024 * 1024)      return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
            return $"{b / (1024.0 * 1024 * 1024):F2} GB";
        }

        private static string FormatDuration(TimeSpan ts)
            => ts.TotalSeconds < 60
                ? $"{ts.TotalSeconds:F1}s"
                : $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
    }

    // ── Progress types ────────────────────────────────────────────────

    public enum BackupPhase
    {
        Collecting, Checksums, Encrypting,
        Compressing, Writing, Verifying, Done
    }

    public sealed class BackupProgressEventArgs : EventArgs
    {
        public BackupPhase Phase   { get; }
        public string      Message { get; }
        public long        Done    { get; }
        public long        Total   { get; }
        public double      Pct     => Total > 0 ? (double)Done / Total * 100.0 : 0;

        public BackupProgressEventArgs(BackupPhase phase, string message, long done, long total)
        {
            Phase = phase; Message = message; Done = done; Total = total;
        }
    }
}
