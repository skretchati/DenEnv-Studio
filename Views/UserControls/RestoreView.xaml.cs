using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DevEnvStudio.Services;
using DevEnvStudio.ViewModels;

namespace DevEnvStudio.Views.UserControls
{
    public partial class RestoreView : UserControl
    {
        public RestoreView()
        {
            InitializeComponent();
            DataContext = DashboardViewModel.Instance;
        }

        // ── Helper: pick a backup file based on RestoreMode setting ──
        private string? PickRestoreTarget()
        {
            var vm = DashboardViewModel.Instance;
            var selected = vm.SelectedBackup;

            if (SettingsService.Instance.RestoreMode == "last")
            {
                var backup = vm.Backups.Count > 0 ? vm.Backups[0] : null;

                if (backup?.IsDevBackup != true)
                {
                    if (selected?.IsDevBackup == true) return selected.FullPath;
                    return vm.PickDevBackupFile();
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

            return vm.PickDevBackupFile();
        }

        // ── Helper: ensure password is cached for protected backups ──
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
                    MessageBox.Show(loc.PasswordWrong, "DevEnv Studio",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return false;
        }

        // ── Event handlers ──────────────────────────────────────────

        private async void RestoreAction_Click(object sender, RoutedEventArgs e)
        {
            var file = PickRestoreTarget();
            if (file == null) return;
            if (!EnsurePasswordForProtectedBackup(file)) return;
            await DashboardViewModel.Instance.RestoreBackupAsync(file);
        }

        // ── Preview: Open Archive ───────────────────────────────────
        private async void OpenPreviewArchive_Click(object sender, RoutedEventArgs e)
        {
            var backup = DashboardViewModel.Instance.SelectedBackup
                      ?? (DashboardViewModel.Instance.Backups.Count > 0
                          ? DashboardViewModel.Instance.Backups[0]
                          : null);

            if (backup?.IsDevBackup != true)
            {
                LogService.Instance.Warn("Open Preview: No valid backup to open.");
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
    }
}
