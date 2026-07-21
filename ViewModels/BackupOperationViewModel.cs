using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DevEnvStudio.Services;
using DevEnvStudio.Services.Backup;
using DevEnvStudio.Services.Backup.Models;

namespace DevEnvStudio.ViewModels
{
    /// <summary>
    /// Dedicated ViewModel for backup/restore operations.
    /// Wires BackupEngine and RestoreEngine progress events to Observable properties.
    /// Shares the existing LogService and DashboardViewModel.Dispatch pattern.
    /// 
    /// Consumed by DashboardViewModel — not instantiated directly by the View.
    /// </summary>
    public sealed class BackupOperationViewModel : ObservableObject
    {
        private static readonly Lazy<BackupOperationViewModel> _instance =
            new(() => new BackupOperationViewModel(), true);
        public static BackupOperationViewModel Instance => _instance.Value;

        private CancellationTokenSource? _cts;

        private BackupOperationViewModel()
        {
            // Wire engine events on construction
            BackupEngine.Instance.ProgressChanged  += OnBackupProgress;
            RestoreEngine.Instance.ProgressChanged += OnRestoreProgress;
        }

        // ══════════════════════════════════════════════════════════════
        //  OBSERVABLE STATE
        // ══════════════════════════════════════════════════════════════

        private bool   _isRunning;
        private bool   _isBackupRunning;
        private bool   _isRestoreRunning;
        private string _operationTitle   = string.Empty;
        private string _operationStatus  = string.Empty;
        private string _currentFile      = string.Empty;
        private double _progressPct;
        private long   _progressDone;
        private long   _progressTotal;
        private string _progressLabel    = string.Empty;
        private string _lastResultMsg    = string.Empty;
        private bool   _lastResultOk;
        private string _operationPhase   = string.Empty;

        public bool   IsRunning         { get => _isRunning;        set => SetField(ref _isRunning, value); }
        public bool   IsBackupRunning   { get => _isBackupRunning;  set => SetField(ref _isBackupRunning, value); }
        public bool   IsRestoreRunning  { get => _isRestoreRunning; set => SetField(ref _isRestoreRunning, value); }
        public string OperationTitle    { get => _operationTitle;   set => SetField(ref _operationTitle, value); }
        public string OperationStatus   { get => _operationStatus;  set => SetField(ref _operationStatus, value); }
        public string CurrentFile       { get => _currentFile;      set => SetField(ref _currentFile, value); }
        public double ProgressPct       { get => _progressPct;      set => SetField(ref _progressPct, value); }
        public long   ProgressDone      { get => _progressDone;     set => SetField(ref _progressDone, value); }
        public long   ProgressTotal     { get => _progressTotal;    set => SetField(ref _progressTotal, value); }
        public string ProgressLabel     { get => _progressLabel;    set => SetField(ref _progressLabel, value); }
        public string LastResultMessage { get => _lastResultMsg;    set => SetField(ref _lastResultMsg, value); }
        public bool   LastResultOk      { get => _lastResultOk;     set => SetField(ref _lastResultOk, value); }
        public string OperationPhase    { get => _operationPhase;   set => SetField(ref _operationPhase, value); }

        // ══════════════════════════════════════════════════════════════
        //  CREATE BACKUP
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Starts a backup for the developer profile.
        /// Called from DashboardViewModel.CreateBackupAsync().
        /// </summary>
        public async Task<BackupOperationResult> RunBackupAsync(
            BackupProfile? profile = null,
            CancellationToken externalCt = default)
        {
            if (IsRunning)
            {
                LogService.Instance.Warn("[ViewModel] Operation already running.");
                return BackupOperationResult.Fail(DateTime.UtcNow, "Operation already in progress.");
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _cts.Token;

            var activeProfile = profile ?? BackupProfile.CreateDeveloperProfile();

            Dispatch(() =>
            {
                IsRunning       = true;
                IsBackupRunning = true;
                OperationTitle  = "Creating Backup";
                OperationStatus = "Initialising…";
                ProgressPct     = 0;
                CurrentFile     = string.Empty;
                OperationPhase  = "Collecting";
            });

            BackupOperationResult result;
            try
            {
                result = await BackupEngine.Instance
                    .CreateBackupAsync(activeProfile, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                Dispatch(() =>
                {
                    IsRunning       = false;
                    IsBackupRunning = false;
                });
            }

            Dispatch(() =>
            {
                LastResultOk      = result.Succeeded;
                LastResultMessage = result.Succeeded
                    ? $"Backup complete in {result.DurationFmt} — {result.FilesProcessed} files"
                    : $"Backup failed: {result.Message}";
                OperationStatus   = LastResultMessage;
                ProgressPct       = result.Succeeded ? 100 : 0;
            });

            // Refresh the backup list in DashboardViewModel
            if (result.Succeeded)
                await RefreshBackupListAsync().ConfigureAwait(false);

            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  RESTORE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Starts restore of the given .devbackup file.
        /// Called from DashboardViewModel.RestoreBackupAsync().
        /// </summary>
        public async Task<BackupOperationResult> RunRestoreAsync(
            string            backupFilePath,
            string?           destinationOverride = null,
            CancellationToken externalCt          = default)
        {
            if (IsRunning)
            {
                LogService.Instance.Warn("[ViewModel] Operation already running.");
                return BackupOperationResult.Fail(DateTime.UtcNow, "Operation already in progress.");
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
            var ct = _cts.Token;

            Dispatch(() =>
            {
                IsRunning        = true;
                IsRestoreRunning = true;
                OperationTitle   = "Restoring Backup";
                OperationStatus  = "Validating archive…";
                ProgressPct      = 0;
                CurrentFile      = string.Empty;
                OperationPhase   = "Validating";
            });

            BackupOperationResult result;
            try
            {
                result = await RestoreEngine.Instance
                    .RestoreBackupAsync(backupFilePath, destinationOverride, ct)
                    .ConfigureAwait(false);
            }
            finally
            {
                Dispatch(() =>
                {
                    IsRunning        = false;
                    IsRestoreRunning = false;
                });
            }

            Dispatch(() =>
            {
                LastResultOk      = result.Succeeded;
                LastResultMessage = result.Succeeded
                    ? $"Restore complete in {result.DurationFmt} — {result.FilesProcessed} files restored"
                    : $"Restore failed: {result.Message}";
                OperationStatus   = LastResultMessage;
                ProgressPct       = result.Succeeded ? 100 : 0;
            });

            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  CANCEL
        // ══════════════════════════════════════════════════════════════

        public void CancelOperation()
        {
            if (_cts == null || !IsRunning) return;
            _cts.Cancel();
            LogService.Instance.Warn("[ViewModel] Operation cancelled by user.");
            Dispatch(() => OperationStatus = "Cancelling…");
        }

        // ══════════════════════════════════════════════════════════════
        //  ENGINE EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════

        private void OnBackupProgress(object? sender, BackupProgressEventArgs e)
        {
            Dispatch(() =>
            {
                OperationPhase  = e.Phase.ToString();
                OperationStatus = e.Message;
                ProgressDone    = e.Done;
                ProgressTotal   = e.Total;
                ProgressPct     = e.Pct;
                ProgressLabel   = e.Total > 0
                    ? $"{FormatBytes(e.Done)} / {FormatBytes(e.Total)}"
                    : e.Message;

                // Mirror to DashboardViewModel.ScanStatus for the existing status bar
                DashboardViewModel.Instance.ScanStatus = e.Message;
            });
        }

        private void OnRestoreProgress(object? sender, RestoreProgressEventArgs e)
        {
            Dispatch(() =>
            {
                OperationPhase  = e.Phase.ToString();
                OperationStatus = e.Message;
                ProgressDone    = e.Done;
                ProgressTotal   = e.Total;
                ProgressPct     = e.Pct;
                ProgressLabel   = e.Total > 0
                    ? $"{e.Done}/{e.Total} files"
                    : e.Message;

                DashboardViewModel.Instance.ScanStatus = e.Message;
            });
        }

        // ══════════════════════════════════════════════════════════════
        //  REFRESH BACKUP LIST
        // ══════════════════════════════════════════════════════════════

        private static async Task RefreshBackupListAsync()
        {
            // Re-trigger the scan for .devbackup files only on Desktop + destination
            var profile = BackupProfile.CreateDeveloperProfile();
            string destDir = profile.ResolvedDestination;

            await Task.Run(() =>
            {
                try
                {
                    foreach (var file in System.IO.Directory
                        .EnumerateFiles(destDir, "*.devbackup",
                            System.IO.SearchOption.TopDirectoryOnly))
                    {
                        var fi   = new System.IO.FileInfo(file);
                        var item = BackupScannerService.Instance
                            .CreateItemFromFile(fi, "DevEnv Studio");

                        Dispatch(() =>
                        {
                            var backups = DashboardViewModel.Instance.Backups;
                            // Avoid duplicates
                            for (int i = 0; i < backups.Count; i++)
                            {
                                if (backups[i].FullPath == item.FullPath)
                                {
                                    backups.RemoveAt(i);
                                    backups.Insert(i, item);
                                    return;
                                }
                            }
                            backups.Insert(0, item);
                            DashboardViewModel.Instance.BackupCount = backups.Count;
                        });
                    }
                }
                catch { /* non-critical */ }
            }).ConfigureAwait(false);
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void Dispatch(Action a)
        {
            if (Application.Current?.Dispatcher is Dispatcher d)
            {
                if (d.CheckAccess()) a();
                else d.BeginInvoke(a, DispatcherPriority.DataBind);
            }
        }

        private static string FormatBytes(long b)
        {
            if (b <= 0)               return "0 B";
            if (b < 1024)             return $"{b} B";
            if (b < 1024 * 1024)      return $"{b / 1024.0:F1} KB";
            if (b < 1024L * 1024 * 1024) return $"{b / (1024.0 * 1024):F1} MB";
            return $"{b / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
