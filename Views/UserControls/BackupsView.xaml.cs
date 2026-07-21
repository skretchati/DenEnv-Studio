using System;
using System.Windows;
using System.Windows.Controls;
using DevEnvStudio.Services;
using DevEnvStudio.ViewModels;

namespace DevEnvStudio.Views.UserControls
{
    public partial class BackupsView : UserControl
    {
        public BackupsView()
        {
            InitializeComponent();
            DataContext = DashboardViewModel.Instance;
        }

        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            await DashboardViewModel.Instance.CreateBackupAsync();
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
    }
}
