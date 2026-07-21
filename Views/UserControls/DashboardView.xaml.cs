using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DevEnvStudio.Services;
using DevEnvStudio.ViewModels;

namespace DevEnvStudio.Views.UserControls
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = DashboardViewModel.Instance;
        }

        // ── Helper: pick a backup file based on RestoreMode setting ──
        private string? PickRestoreTarget()
        {
            var selected = DashboardViewModel.Instance.SelectedBackup;

            if (SettingsService.Instance.RestoreMode == "last")
            {
                var backup = DashboardViewModel.Instance.Backups.Count > 0
                    ? DashboardViewModel.Instance.Backups[0]
                    : null;

                if (backup?.IsDevBackup != true)
                {
                    if (selected?.IsDevBackup == true) return selected.FullPath;
                    return DashboardViewModel.Instance.PickDevBackupFile();
                }

                var loc = LocalizationService.Instance;
                string archiveName = System.IO.Path.GetFileName(backup.Name);
                string msg = $"{loc.RestoreConfirm}\n\n{archiveName}";
                string title = loc.RestoreTitle;

                var answer = MessageBox.Show(msg, title,
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                return answer == MessageBoxResult.Yes ? backup.FullPath : null;
            }

            if (selected?.IsDevBackup == true)
            {
                string? initDir = System.IO.Path.GetDirectoryName(selected.FullPath);
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select a DevEnvStudio Backup",
                    Filter = "DevEnvStudio Backup (*.devbackup)|*.devbackup|All files (*.*)|*.*",
                    DefaultExt = ".devbackup",
                    InitialDirectory = initDir,
                    Multiselect = false
                };
                bool? result = dlg.ShowDialog();
                return result == true ? dlg.FileName : null;
            }

            return DashboardViewModel.Instance.PickDevBackupFile();
        }

        // ── Helper: if the target backup is password-protected, ensure the
        //           password is cached (prompt user if needed).
        private bool EnsurePasswordForProtectedBackup(string filePath)
        {
            bool isProtected = false;

            foreach (var item in DashboardViewModel.Instance.Backups)
            {
                if (item.FullPath == filePath)
                {
                    isProtected = item.IsPasswordProtected;
                    break;
                }
            }

            if (!isProtected)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                if (!fileName.Contains("[PROTECTED]"))
                    return true;
            }

            if (Services.PasswordCache.Instance.HasPassword)
                return true;

            var loc = Services.LocalizationService.Instance;
            var dlg = new Views.PasswordDialog
            {
                Owner = Window.GetWindow(this),
                Title = loc.PasswordPromptTitle
            };

            if (dlg.ShowDialog() == true)
            {
                string pwd = dlg.Password;
                if (Services.PasswordProtectionService.Instance.VerifyPassword(pwd))
                {
                    Services.PasswordCache.Instance.SetPassword(pwd);
                    return true;
                }
                else
                {
                    MessageBox.Show(
                        loc.PasswordWrong,
                        "DevEnv Studio",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }
            }

            return false;
        }

        // ── Header: Create Backup ─────────────────────────────────────
        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            await DashboardViewModel.Instance.CreateBackupAsync();
        }

        // ── Header: Restore Backup ──────────────────────────────────────
        private async void RestoreFromHeader_Click(object sender, RoutedEventArgs e)
        {
            var file = PickRestoreTarget();
            if (file == null) return;
            if (!EnsurePasswordForProtectedBackup(file)) return;
            DashboardViewModel.Instance.SelectedBackupName = System.IO.Path.GetFileName(file);
            await DashboardViewModel.Instance.RestoreBackupAsync(file);
        }

        // ── Restore Preview: Open Archive ────────────────────────────────
        private async void OpenPreviewArchive_Click(object sender, RoutedEventArgs e)
        {
            var backup = DashboardViewModel.Instance.SelectedBackup
                      ?? (DashboardViewModel.Instance.Backups.Count > 0
                          ? DashboardViewModel.Instance.Backups[0]
                          : null);

            if (backup?.IsDevBackup != true)
            {
                Services.LogService.Instance.Warn("Open Preview: No valid backup to open.");
                return;
            }

            if (!EnsurePasswordForProtectedBackup(backup.FullPath)) return;

            DashboardViewModel.Instance.SelectedBackupName = System.IO.Path.GetFileName(backup.Name);

            await DashboardViewModel.Instance.RestoreBackupAsync(
                backup.FullPath,
                System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"DevEnvStudio_Preview_{System.IO.Path.GetFileNameWithoutExtension(backup.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}"));
        }

        // ── Action: Create Backup ────────────────────────────────────────
        private async void CreateBackupAction_Click(object sender, RoutedEventArgs e)
        {
            await DashboardViewModel.Instance.CreateBackupAsync();
        }

        // ── Action: Restore Backup ───────────────────────────────────────
        private async void RestoreAction_Click(object sender, RoutedEventArgs e)
        {
            var file = PickRestoreTarget();
            if (file == null) return;
            if (!EnsurePasswordForProtectedBackup(file)) return;
            await DashboardViewModel.Instance.RestoreBackupAsync(file);
        }

        // ── Action: Open Archive ─────────────────────────────────────────
        private async void OpenArchiveAction_Click(object sender, RoutedEventArgs e)
        {
            var backup = DashboardViewModel.Instance.SelectedBackup;
            if (backup?.IsDevBackup != true)
            {
                var loc = Services.LocalizationService.Instance;
                MessageBox.Show("Select a backup from the Recent Backups list first.",
                                loc.AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!EnsurePasswordForProtectedBackup(backup.FullPath)) return;
            await DashboardViewModel.Instance.OpenArchiveAsync(backup.FullPath);
        }

        // ── Action: Compare Backups ──────────────────────────────────────
        private async void CompareBackupsAction_Click(object sender, RoutedEventArgs e)
        {
            var (pathA, pathB) = DashboardViewModel.Instance.PickTwoDevBackupFiles();

            if (pathA == null)
            {
                Services.LogService.Instance.Warn("Compare cancelled — no files selected.");
                return;
            }

            if (pathB == null)
            {
                Services.LogService.Instance.Warn("Compare cancelled — second file not selected.");
                return;
            }

            var result = await Task.Run(() =>
                DashboardViewModel.Instance.CompareBackupsAsync(pathA, pathB));

            MessageBox.Show(result, "Backup Comparison",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
