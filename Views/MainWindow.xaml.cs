using System;
using System.Windows;
using System.Windows.Controls;
using DevEnvStudio.Services;
using DevEnvStudio.ViewModels;

namespace DevEnvStudio.Views
{
    public partial class MainWindow : Window
    {
        private bool _isPageReady;

        public MainWindow()
        {
            InitializeComponent();

            // After InitializeComponent, the visual tree is ready.
            _isPageReady = true;

            // Start async init AFTER the window is fully rendered
            ContentRendered += OnContentRendered;
        }

        private async void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;

            // Password prompt is now deferred — shown only when restoring a protected backup.

            try
            {
                await DashboardViewModel.Instance.InitializeAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error(
                    string.Format(LocalizationService.Instance.InitError, ex.Message));
            }
        }

        private void PromptForPassword()
        {
            var loc = Services.LocalizationService.Instance;
            var dlg = new Views.PasswordDialog
            {
                Owner = this,
                Title = loc.PasswordPromptTitle
            };
            if (dlg.ShowDialog() == true)
            {
                string pwd = dlg.Password;
                if (Services.PasswordProtectionService.Instance.VerifyPassword(pwd))
                {
                    Services.PasswordCache.Instance.SetPassword(pwd);
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        loc.PasswordWrong,
                        "DevEnv Studio",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            DashboardViewModel.Instance.CancelScan();
            base.OnClosed(e);
        }

        // ── Window controls ─────────────────────────────────────────

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e)
            => Close();

        // ── Navigation ──────────────────────────────────────────────

        private void NavDashboard_Checked(object sender, RoutedEventArgs e)
            => ShowPage(DashboardPage);

        private void NavBackups_Checked(object sender, RoutedEventArgs e)
            => ShowPage(BackupsPage);

        private void NavRestore_Checked(object sender, RoutedEventArgs e)
            => ShowPage(RestorePage);

        private void NavSettings_Checked(object sender, RoutedEventArgs e)
            => ShowPage(SettingsPage);

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPageReady) return;
            NavSettings.IsChecked = true;
            ShowPage(SettingsPage);
        }

        private void ShowPage(UserControl page)
        {
            if (!_isPageReady || page == null) return;
            DashboardPage.Visibility = Visibility.Collapsed;
            BackupsPage.Visibility   = Visibility.Collapsed;
            RestorePage.Visibility   = Visibility.Collapsed;
            SettingsPage.Visibility  = Visibility.Collapsed;
            page.Visibility = Visibility.Visible;
        }

        // ── Help / About → open GitHub repo ──────────────────────────

        private void HelpButton_Click(object sender, RoutedEventArgs e)
            => OpenGitHub();

        private void AboutButton_Click(object sender, RoutedEventArgs e)
            => OpenGitHub();

        private static void OpenGitHub()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/skretchati/DenEnv-Studio",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"Failed to open GitHub: {ex.Message}");
            }
        }
    }
}