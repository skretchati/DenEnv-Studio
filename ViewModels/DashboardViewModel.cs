using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DevEnvStudio.Services;
using DevEnvStudio.Services.Backup;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.ViewModels
{
    public sealed class DashboardViewModel : ObservableObject
    {
        // ── Singleton ────────────────────────────────────────────────
        private static readonly Lazy<DashboardViewModel> _instance =
            new(() => new DashboardViewModel(), true);
        public static DashboardViewModel Instance => _instance.Value;

        // ── Scan cancellation ───────────────────────────────────────
        private CancellationTokenSource? _cts;

        // ── Constructor ─────────────────────────────────────────────
        private DashboardViewModel()
        {
            Backups    = new ObservableCollection<BackupItem>();
            LogEntries = LogService.Instance.Entries;
        }

        // ══════════════════════════════════════════════════════════════
        //  OBSERVABLE COLLECTIONS
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<BackupItem>  Backups    { get; }
        public ObservableCollection<Services.LogEntry> LogEntries { get; }

        // ══════════════════════════════════════════════════════════════
        //  SYSTEM INFORMATION
        // ══════════════════════════════════════════════════════════════

        private string _osVersion    = "Scanning…";
        private string _cpuName      = "Scanning…";
        private string _ramTotal     = "Scanning…";
        private string _storageUsed  = "—";
        private string _storageTotal = "—";
        private string _storageFree  = "—";
        private string _storagePct   = "—";
        private string _driveLetter  = "—";
        private string _driveType    = "—";

        public string OsVersion    { get => _osVersion;    set => SetField(ref _osVersion, value); }
        public string CpuName      { get => _cpuName;      set => SetField(ref _cpuName, value); }
        public string RamTotal     { get => _ramTotal;     set => SetField(ref _ramTotal, value); }
        public string StorageUsed  { get => _storageUsed;  set => SetField(ref _storageUsed, value); }
        public string StorageTotal { get => _storageTotal; set => SetField(ref _storageTotal, value); }
        public string StorageFree  { get => _storageFree;  set => SetField(ref _storageFree, value); }
        public string StoragePct   { get => _storagePct;   set => SetField(ref _storagePct, value); }
        public string DriveLetter  { get => _driveLetter;  set => SetField(ref _driveLetter, value); }
        public string DriveType    { get => _driveType;    set => SetField(ref _driveType, value); }

        // Combined display helpers
        public string StorageDisplay =>
            $"{StorageUsed} / {StorageTotal}";

        // ══════════════════════════════════════════════════════════════
        //  STAT CARD NUMBERS
        // ══════════════════════════════════════════════════════════════

        private int    _backupCount   = 0;
        private string _totalSize     = "0 B";
        private string _scanStatus    = "Ready";
        private bool   _isScanning    = false;
        private double _storagePctNum = 0;
        private string _storagePctDisplay = "0%";

        public int    BackupCount        { get => _backupCount;        set { SetField(ref _backupCount, value); OnPropertyChanged(nameof(BackupCountDisplay)); } }
        public string TotalBackupSize    { get => _totalSize;          set => SetField(ref _totalSize, value); }
        public string ScanStatus         { get => _scanStatus;         set => SetField(ref _scanStatus, value); }
        public bool   IsScanning         { get => _isScanning;         set => SetField(ref _isScanning, value); }
        public double StoragePctNum      { get => _storagePctNum;      set => SetField(ref _storagePctNum, value); }
        public string StoragePctDisplay  { get => _storagePctDisplay;  set => SetField(ref _storagePctDisplay, value); }
        public string BackupCountDisplay => _backupCount.ToString();

        // Last backup date label
        private string _lastBackupDate = "Never";
        public  string LastBackupDate  { get => _lastBackupDate; set => SetField(ref _lastBackupDate, value); }

        // Scan progress current file path
        private string _scanCurrentPath = string.Empty;
        public  string ScanCurrentPath { get => _scanCurrentPath; set => SetField(ref _scanCurrentPath, value); }

        private string _scanPhase = string.Empty;
        public  string ScanPhase  { get => _scanPhase; set => SetField(ref _scanPhase, value); }

        // Name of the backup currently selected for restore (shown in status bar)
        private string _selectedBackupName = string.Empty;
        public  string SelectedBackupName { get => _selectedBackupName; set => SetField(ref _selectedBackupName, value); }

        // The currently highlighted backup in the Recent Backups list (clicked by user)
        private BackupItem? _selectedBackup;
        public BackupItem? SelectedBackup
        {
            get => _selectedBackup;
            set
            {
                if (SetField(ref _selectedBackup, value) && value != null)
                {
                    SelectedBackupName = System.IO.Path.GetFileName(value.Name);
                    OnPropertyChanged(nameof(SelectedBackupIndex));
                }
            }
        }

        private int _selectedBackupIndex = -1;
        public int SelectedBackupIndex
        {
            get => _selectedBackupIndex;
            set
            {
                if (SetField(ref _selectedBackupIndex, value) &&
                    value >= 0 && value < Backups.Count)
                {
                    SelectedBackup = Backups[value];
                }
                else if (value < 0)
                {
                    SelectedBackup = null;
                    if (!string.IsNullOrEmpty(_selectedBackupName))
                    {
                        _selectedBackupName = string.Empty;
                        OnPropertyChanged(nameof(SelectedBackupName));
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  STARTUP — called from MainWindow after Loaded
        // ══════════════════════════════════════════════════════════════

        public async Task InitializeAsync()
        {
            _cts = new CancellationTokenSource();

            // Run system info and storage scan in parallel
            var sysTask     = LoadSystemInfoAsync();
            var storageTask = LoadStorageAsync();

            await Task.WhenAll(sysTask, storageTask).ConfigureAwait(false);

            // Start backup scanning (fire-and-forget with cancellation)
            _ = ScanBackupsAsync(_cts.Token);

            // Apply crash-log setting after settings are loaded from disk
            _ = System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => ((App)System.Windows.Application.Current).ApplyCrashLogSetting());
        }

        public void CancelScan()
        {
            _cts?.Cancel();
            IsScanning = false;
            ScanStatus = "Scan cancelled";
        }

        // ══════════════════════════════════════════════════════════════
        //  BACKUP / RESTORE ENTRY POINTS
        //  Delegates to BackupOperationViewModel — keeps DashboardViewModel
        //  focused on scan/display concerns only.
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Exposed to the View for binding the operation panel.
        /// </summary>
        public BackupOperationViewModel BackupOperation
            => BackupOperationViewModel.Instance;

        /// <summary>
        /// Creates a backup using the default developer profile.
        /// Safe to call from UI thread — fully async.
        /// </summary>
        public async Task CreateBackupAsync(CancellationToken ct = default)
{
    Dispatch(() =>
    {
        IsScanning = true;
        ScanStatus = "Creating backup...";
    });

    try
    {
        var result = await BackupOperationViewModel.Instance
            .RunBackupAsync(null, ct)
            .ConfigureAwait(false);

        Dispatch(() =>
        {
            ScanStatus = result.Succeeded
                ? $"✓ Backup complete — {result.FilesProcessed} files in {result.DurationFmt}"
                : $"✗ Backup failed: {result.Message}";
        });
    }
    finally
    {
        Dispatch(() => IsScanning = false);
    }
}

        /// <summary>
        /// Restores the given .devbackup file.
        /// Full validation runs before any files are touched.
        /// </summary>
        public async Task RestoreBackupAsync(
            string backupFilePath,
            string? destinationOverride = null,
            CancellationToken ct = default)
        {
            Dispatch(() =>
            {
                IsScanning = true;
                ScanStatus = $"Restoring {System.IO.Path.GetFileName(backupFilePath)}…";
            });
            try
            {
                var result = await BackupOperationViewModel.Instance
                    .RunRestoreAsync(backupFilePath, destinationOverride, ct)
                    .ConfigureAwait(false);

                Dispatch(() =>
                {
                    ScanStatus = result.Succeeded
                        ? $"✓ Restore complete — {result.FilesProcessed} files in {result.DurationFmt}"
                        : $"✗ Restore failed: {result.Message}";
                });
            }
            finally
            {
                Dispatch(() => IsScanning = false);
            }
        }

        /// <summary>
        /// Validates a .devbackup file and returns the result for UI display.
        /// </summary>
        public async Task<ValidationResult> ValidateBackupAsync(
            string backupFilePath, CancellationToken ct = default)
        {
            Dispatch(() => ScanStatus = $"Validating {System.IO.Path.GetFileName(backupFilePath)}…");
            var result = await ArchiveValidationService.Instance
                .ValidateAsync(backupFilePath, ct).ConfigureAwait(false);
            Dispatch(() => ScanStatus = result.IsValid
                ? $"✓ Archive valid: {System.IO.Path.GetFileName(backupFilePath)}"
                : $"✗ Validation failed: {result.Message}");
            return result;
        }

        /// <summary>Cancels any running backup/restore operation.</summary>
        public void CancelOperation()
        {
            _cts?.Cancel();
            BackupOperationViewModel.Instance.CancelOperation();
        }

        // ══════════════════════════════════════════════════════════════
        //  OPEN ARCHIVE — decrypt payload, open folder in Explorer
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens a .devbackup archive: decrypts its payload into a temporary folder
        /// and opens that folder in Windows Explorer.
        /// If <paramref name="backupFilePath"/> is null, uses SelectedBackup
        /// (falling back to the most recent backup).
        /// </summary>
        public async Task OpenArchiveAsync(string? backupFilePath = null, CancellationToken ct = default)
        {
            string archivePath;
            if (!string.IsNullOrWhiteSpace(backupFilePath))
            {
                archivePath = backupFilePath;
            }
            else if (SelectedBackup?.IsDevBackup == true && SelectedBackup.Manifest != null)
            {
                archivePath = SelectedBackup.FullPath;
            }
            else if (Backups.Count > 0 && Backups[0].IsDevBackup == true && Backups[0].Manifest != null)
            {
                archivePath = Backups[0].FullPath;
            }
            else
            {
                Dispatch(() => ScanStatus = "No archive to open — select a backup first.");
                return;
            }
            string archiveName = System.IO.Path.GetFileNameWithoutExtension(archivePath);
            string extractRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"DevEnvStudio_{archiveName}_{DateTime.Now:yyyyMMdd_HHmmss}");

            Dispatch(() => ScanStatus = $"Opening archive: {archiveName}…");

            try
            {
                System.IO.Directory.CreateDirectory(extractRoot);

                // Read unencrypted header to get manifest + offsets
                var header = await BackupContainerService.Instance
                    .ReadHeaderAsync(archivePath, ct).ConfigureAwait(false);

                // Derive wrapping key from BackupId in metadata
                byte[] wrappingKey = EncryptionService.Instance.DeriveWrappingKey(header.Metadata.BackupId);

                // Read encrypted session key
                byte[] encSessKey = await BackupContainerService.Instance
                    .ReadEncryptedSessionKeyAsync(archivePath, header, ct).ConfigureAwait(false);

                // Remove password layer if active
                if (Services.PasswordCache.Instance.HasPassword)
                {
                    string? pwd = Services.PasswordCache.Instance.GetPassword();
                    if (!string.IsNullOrWhiteSpace(pwd))
                    {
                        try
                        {
                            encSessKey = Services.PasswordProtectionService.Instance
                                .UnwrapPasswordLayer(encSessKey, pwd);
                        }
                        catch (System.Security.Cryptography.CryptographicException)
                        {
                            Dispatch(() => ScanStatus =
                                "✗ Wrong password — archive was protected with a different password.");
                            LogService.Instance.Error(
                                "[OpenArchive] Password layer removal failed — wrong password.");
                            EncryptionService.Instance.WipeKey(wrappingKey);
                            return;
                        }
                    }
                }

                // Decrypt session key
                byte[] sessionKey;
                try
                {
                    sessionKey = EncryptionService.Instance.DecryptSessionKey(encSessKey, wrappingKey);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    Dispatch(() => ScanStatus =
                        "✗ Failed to decrypt archive — wrong password or corrupted backup.");
                    LogService.Instance.Error(
                        "[OpenArchive] Session key decryption failed — key length " +
                        $"{encSessKey.Length}, password-cached={Services.PasswordCache.Instance.HasPassword}.");
                    EncryptionService.Instance.WipeKey(wrappingKey);
                    return;
                }

                try
                {
                    // Open and decrypt payload stream
                    using var payloadStream = BackupContainerService.Instance.OpenPayloadStream(archivePath, header);
                    using var decryptedPayload = new System.IO.MemoryStream();

                    await EncryptionService.Instance.DecryptStreamAsync(
                        payloadStream, decryptedPayload, sessionKey,
                        bytesProgress: null, ct: ct).ConfigureAwait(false);

                    decryptedPayload.Position = 0;

                    // Decompress and write files
                    int fileCount = 0;
                    await CompressionService.Instance.DecompressFromStreamAsync(
                        decryptedPayload,
                        extractRoot,
                        header.Checksums,
                        onFile: (entry, err) =>
                        {
                            if (err == null) Interlocked.Increment(ref fileCount);
                        },
                        ct: ct).ConfigureAwait(false);

                    Dispatch(() => ScanStatus =
                        $"✓ Archive opened: {fileCount} files extracted to {extractRoot}");

                    // Open in Explorer
                    System.Diagnostics.Process.Start("explorer.exe", extractRoot);
                }
                finally
                {
                    EncryptionService.Instance.WipeKey(sessionKey);
                }

                EncryptionService.Instance.WipeKey(wrappingKey);
            }
            catch (OperationCanceledException)
            {
                Dispatch(() => ScanStatus = "Archive open cancelled.");
            }
            catch (Exception ex)
            {
                Dispatch(() => ScanStatus = $"✗ Failed to open archive: {ex.Message}");
                LogService.Instance.Error($"[OpenArchive] {ex}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  COMPARE BACKUPS — diff two .devbackup manifests
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Compares the unencrypted manifests of two .devbackup archives
        /// and returns a human-readable diff summary.
        /// </summary>
        public async Task<string> CompareBackupsAsync(
            string pathA, string pathB, CancellationToken ct = default)
        {
            try
            {
                var headerA = await BackupContainerService.Instance
                    .ReadHeaderAsync(pathA, ct).ConfigureAwait(false);
                var headerB = await BackupContainerService.Instance
                    .ReadHeaderAsync(pathB, ct).ConfigureAwait(false);

                var filesA = new HashSet<string>(
                    headerA.Manifest.Entries.Select(e => e.RelativePath.Replace('\\', '/').Trim('/')),
                    StringComparer.OrdinalIgnoreCase);
                var filesB = new HashSet<string>(
                    headerB.Manifest.Entries.Select(e => e.RelativePath.Replace('\\', '/').Trim('/')),
                    StringComparer.OrdinalIgnoreCase);

                var onlyA = filesA.Except(filesB).ToList();
                var onlyB = filesB.Except(filesA).ToList();
                var common = filesA.Intersect(filesB).ToList();

                // Size diff for common files
                var sizeDiff = new List<(string path, long sizeA, long sizeB)>();
                foreach (var path in common)
                {
                    var ea = headerA.Manifest.Entries.First(e =>
                        e.RelativePath.Replace('\\', '/').Trim('/').Equals(path, StringComparison.OrdinalIgnoreCase));
                    var eb = headerB.Manifest.Entries.First(e =>
                        e.RelativePath.Replace('\\', '/').Trim('/').Equals(path, StringComparison.OrdinalIgnoreCase));
                    if (ea.SizeBytes != eb.SizeBytes)
                        sizeDiff.Add((path, ea.SizeBytes, eb.SizeBytes));
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Compare: {System.IO.Path.GetFileName(pathA)} vs {System.IO.Path.GetFileName(pathB)}");
                sb.AppendLine();
                sb.AppendLine($"  A: {headerA.Metadata.BackupName} — {headerA.Manifest.FileCount} files, {FormatCompareBytes(headerA.Manifest.TotalSizeBytes())}");
                sb.AppendLine($"  B: {headerB.Metadata.BackupName} — {headerB.Manifest.FileCount} files, {FormatCompareBytes(headerB.Manifest.TotalSizeBytes())}");
                sb.AppendLine();

                if (onlyA.Count > 0)
                {
                    sb.AppendLine($"  Only in A ({onlyA.Count}):");
                    foreach (var f in onlyA.Take(10))
                        sb.AppendLine($"    + {f}");
                    if (onlyA.Count > 10)
                        sb.AppendLine($"    … and {onlyA.Count - 10} more");
                }

                if (onlyB.Count > 0)
                {
                    sb.AppendLine($"  Only in B ({onlyB.Count}):");
                    foreach (var f in onlyB.Take(10))
                        sb.AppendLine($"    - {f}");
                    if (onlyB.Count > 10)
                        sb.AppendLine($"    … and {onlyB.Count - 10} more");
                }

                if (onlyA.Count == 0 && onlyB.Count == 0)
                    sb.AppendLine("  ✓ Identical file sets.");

                if (sizeDiff.Count > 0)
                {
                    sb.AppendLine($"  Size changes ({sizeDiff.Count}):");
                    foreach (var (path, sa, sb2) in sizeDiff.Take(8))
                        sb.AppendLine($"    ~ {path}: {FormatCompareBytes(sa)} → {FormatCompareBytes(sb2)}");
                    if (sizeDiff.Count > 8)
                        sb.AppendLine($"    … and {sizeDiff.Count - 8} more");
                }

                sb.AppendLine();
                sb.AppendLine($"  Common files: {common.Count}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Comparison failed: {ex.Message}";
            }
        }

        private static string FormatCompareBytes(long b)
        {
            if (b < 1024L)               return $"{b} B";
            if (b < 1024L * 1024)        return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
            return $"{b / (1024.0 * 1024 * 1024):F2} GB";
        }

        // ══════════════════════════════════════════════════════════════
        //  PICK FILE DIALOG — for Restore and Compare
        // ══════════════════════════════════════════════════════════════

        /// <summary>Shows OpenFileDialog filtered to .devbackup files.</summary>
        public string? PickDevBackupFile()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select a DevEnvStudio Backup",
                Filter = "DevEnvStudio Backup (*.devbackup)|*.devbackup|All files (*.*)|*.*",
                DefaultExt = ".devbackup",
                Multiselect = false
            };

            bool? result = dlg.ShowDialog();
            return result == true ? dlg.FileName : null;
        }

        /// <summary>Shows OpenFileDialog for two .devbackup files to compare.</summary>
        public (string? pathA, string? pathB) PickTwoDevBackupFiles()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select the FIRST backup to compare",
                Filter = "DevEnvStudio Backup (*.devbackup)|*.devbackup|All files (*.*)|*.*",
                DefaultExt = ".devbackup",
                Multiselect = false
            };

            // ── First pick ──
            bool? r1 = dlg.ShowDialog();
            if (r1 != true) return (null, null);

            // Capture path A BEFORE reusing the same dialog instance,
            // otherwise FileName is overwritten by the second pick.
            string pathA = dlg.FileName;

            // ── Second pick · fresh dialog to reset state safely ──
            var dlg2 = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select the SECOND backup to compare",
                Filter = "DevEnvStudio Backup (*.devbackup)|*.devbackup|All files (*.*)|*.*",
                DefaultExt = ".devbackup",
                Multiselect = false
            };

            bool? r2 = dlg2.ShowDialog();
            string? pathB = r2 == true ? dlg2.FileName : null;

            return (pathA, pathB);
        }

        // ══════════════════════════════════════════════════════════════
        //  SYSTEM INFO
        // ══════════════════════════════════════════════════════════════

        private async Task LoadSystemInfoAsync()
        {
            try
            {
                LogService.Instance.Info("Reading system information…");
                var snap = await SystemInfoService.Instance.GetSnapshotAsync()
                    .ConfigureAwait(false);

                Dispatch(() =>
                {
                    OsVersion    = snap.OsVersion;
                    CpuName      = snap.CpuName;
                    RamTotal     = snap.RamTotal;
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"System info error: {ex.Message}");
                Dispatch(() =>
                {
                    OsVersion = Environment.OSVersion.VersionString;
                    CpuName   = "Unknown CPU";
                    RamTotal  = "Unknown";
                });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  STORAGE
        // ══════════════════════════════════════════════════════════════

        private async Task LoadStorageAsync()
        {
            try
            {
                var drive = await StorageService.Instance.GetPrimaryDriveAsync()
                    .ConfigureAwait(false);

                if (drive == null) return;

                Dispatch(() =>
                {
                    StorageUsed         = drive.UsedFmt;
                    StorageTotal        = drive.TotalFmt;
                    StorageFree         = drive.FreeFmt;
                    StoragePctDisplay   = drive.UsedPctFmt;
                    StoragePctNum       = Math.Clamp(drive.UsedPct, 0, 100);
                    DriveLetter         = drive.Letter;
                    DriveType           = drive.Format;
                    OnPropertyChanged(nameof(StorageDisplay));
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Storage scan error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  BACKUP SCANNER
        // ══════════════════════════════════════════════════════════════

        private async Task ScanBackupsAsync(CancellationToken ct)
        {
            Dispatch(() =>
            {
                IsScanning = true;
                ScanStatus = "Scanning for backups…";
            });

            long totalBytes = 0;
            var  throttle   = DateTime.UtcNow;

            try
            {
                await BackupScannerService.Instance.ScanAsync(
                    onFound: item =>
                    {
                        if (ct.IsCancellationRequested) return;

                        Dispatch(() =>
                        {
                            // Insert newest-first, keep max 200 in the observable list
                            int insertAt = 0;
                            for (int i = 0; i < Backups.Count; i++)
                            {
                                if (Backups[i].DateRaw <= item.DateRaw) break;
                                insertAt = i + 1;
                            }
                            if (Backups.Count < 200)
                                Backups.Insert(Math.Min(insertAt, Backups.Count), item);
                            else if (insertAt < 200)
                            {
                                Backups.RemoveAt(Backups.Count - 1);
                                Backups.Insert(insertAt, item);
                            }
                        });

                        Interlocked.Add(ref totalBytes, item.SizeBytes);

                        // Throttle stat-card updates to max 4 per second
                        var now = DateTime.UtcNow;
                        if ((now - throttle).TotalMilliseconds > 250)
                        {
                            throttle = now;
                            int cnt   = Backups.Count;
                            long snap = Interlocked.Read(ref totalBytes);
                            string newest = Backups.Count > 0
                                ? Backups[0].Date
                                : "None found";

                            Dispatch(() =>
                            {
                                BackupCount     = Backups.Count;
                                TotalBackupSize = DriveSnapshot.FormatBytes(snap);
                                LastBackupDate  = newest;
                            });
                        }
                    },
                    onProgress: prog =>
                    {
                        if (ct.IsCancellationRequested) return;
                        Dispatch(() =>
                        {
                            ScanCurrentPath = prog.CurrentPath;
                            ScanPhase       = prog.Phase;
                            ScanStatus      = $"Scanning {prog.Phase}… ({prog.FoundCount} found)";
                        });
                    },
                    ct: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* normal */ }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Scan error: {ex.Message}");
            }
            finally
            {
                Dispatch(() =>
                {
                    IsScanning      = false;
                    BackupCount     = Backups.Count;
                    TotalBackupSize = DriveSnapshot.FormatBytes(totalBytes);
                    LastBackupDate  = Backups.Count > 0 ? Backups[0].Date : "None found";
                    ScanStatus      = Backups.Count > 0
                        ? $"Scan complete — {Backups.Count} backup(s) found"
                        : "Scan complete — no backups found";
                    ScanCurrentPath = string.Empty;
                    ScanPhase       = string.Empty;
                });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void Dispatch(Action a)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
            {
                if (d.CheckAccess())
                    a();
                else
                    d.BeginInvoke(a, DispatcherPriority.DataBind);
            }
        }
    }
}
