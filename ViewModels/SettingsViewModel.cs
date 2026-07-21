using System.Windows.Input;
using DevEnvStudio.Services;

namespace DevEnvStudio.ViewModels
{
    /// <summary>
    /// ViewModel for the Settings page.
    /// Binds directly to SettingsService and LocalizationService.
    /// </summary>
    public sealed class SettingsViewModel : ObservableObject
    {
        private static readonly System.Lazy<SettingsViewModel> _instance =
            new(() => new SettingsViewModel(), true);
        public static SettingsViewModel Instance => _instance.Value;

        private SettingsViewModel()
        {
            SavePasswordCommand = new RelayCommand(_ => SavePassword());
        }

        // ══════════════════════════════════════════════════════════════
        //  LOCALIZATION  (exposed for XAML binding)
        // ══════════════════════════════════════════════════════════════

        public LocalizationService Loc => LocalizationService.Instance;

        // ══════════════════════════════════════════════════════════════
        //  LANGUAGE
        // ══════════════════════════════════════════════════════════════

        public bool IsEnglish
        {
            get => SettingsService.Instance.Language == "en";
            set { if (value) LocalizationService.Instance.SetLanguage("en"); OnPropertyChanged(nameof(IsRussian)); }
        }

        public bool IsRussian
        {
            get => SettingsService.Instance.Language == "ru";
            set { if (value) LocalizationService.Instance.SetLanguage("ru"); OnPropertyChanged(nameof(IsEnglish)); }
        }

        // ══════════════════════════════════════════════════════════════
        //  PASSWORD PROTECTION
        // ══════════════════════════════════════════════════════════════

        private bool _passwordEnabled;
        public bool PasswordEnabled
        {
            get => _passwordEnabled;
            set
            {
                SetField(ref _passwordEnabled, value);
                SettingsService.Instance.PasswordProtectionEnabled = value;
                OnPropertyChanged(nameof(IsPasswordConfigured));
            }
        }

        private string _passwordText = string.Empty;
        public string PasswordText
        {
            get => _passwordText;
            set => SetField(ref _passwordText, value);
        }

        private string _passwordConfirm = string.Empty;
        public string PasswordConfirm
        {
            get => _passwordConfirm;
            set => SetField(ref _passwordConfirm, value);
        }

        private string _passwordMessage = string.Empty;
        public string PasswordMessage
        {
            get => _passwordMessage;
            set => SetField(ref _passwordMessage, value);
        }

        private bool _passwordMessageVisible;
        public bool PasswordMessageVisible
        {
            get => _passwordMessageVisible;
            set => SetField(ref _passwordMessageVisible, value);
        }

        /// <summary>True when a password is already set in settings.</summary>
        public bool IsPasswordConfigured =>
            !string.IsNullOrWhiteSpace(SettingsService.Instance.PasswordHash);

        // ── Save Password ──────────────────────────────────────────────

        public ICommand SavePasswordCommand { get; }

        private void SavePassword()
        {
            PasswordMessageVisible = false;
            PasswordMessage = string.Empty;

            // If disabling — clear the stored hash
            if (!PasswordEnabled)
            {
                SettingsService.Instance.PasswordHash = string.Empty;
                SettingsService.Instance.PasswordProtectionEnabled = false;
                Services.PasswordCache.Instance.Clear();
                PasswordText = string.Empty;
                PasswordConfirm = string.Empty;
                OnPropertyChanged(nameof(IsPasswordConfigured));
                return;
            }

            // If enabling — validate and save
            if (string.IsNullOrWhiteSpace(PasswordText))
            {
                PasswordMessage = "Password cannot be empty.";
                PasswordMessageVisible = true;
                return;
            }

            if (PasswordText != PasswordConfirm)
            {
                PasswordMessage = LocalizationService.Instance.PasswordMismatch;
                PasswordMessageVisible = true;
                return;
            }

            if (PasswordText.Length < 4)
            {
                PasswordMessage = "Password must be at least 4 characters.";
                PasswordMessageVisible = true;
                return;
            }

            string hash = PasswordProtectionService.Instance.HashPassword(PasswordText);
            SettingsService.Instance.PasswordHash = hash;
            SettingsService.Instance.PasswordProtectionEnabled = true;
            Services.PasswordCache.Instance.SetPassword(PasswordText);

            PasswordMessage = Loc.PasswordSaved;
            PasswordMessageVisible = true;
            OnPropertyChanged(nameof(IsPasswordConfigured));
        }

        // ══════════════════════════════════════════════════════════════
        //  CRASH DIAGNOSTIC LOG
        // ══════════════════════════════════════════════════════════════

        private bool _enableCrashLog;
        public bool EnableCrashLog
        {
            get => _enableCrashLog;
            set
            {
                SetField(ref _enableCrashLog, value);
                SettingsService.Instance.EnableCrashLog = value;
                // Apply immediately: close or re-open the diagnostic log
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    () => ((App)System.Windows.Application.Current).ApplyCrashLogSetting());
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  RESTORE MODE
        // ══════════════════════════════════════════════════════════════

        public bool RestoreModeAsk
        {
            get => SettingsService.Instance.RestoreMode == "ask";
            set { if (value) SettingsService.Instance.RestoreMode = "ask"; OnPropertyChanged(nameof(RestoreModeLast)); }
        }

        public bool RestoreModeLast
        {
            get => SettingsService.Instance.RestoreMode == "last";
            set { if (value) SettingsService.Instance.RestoreMode = "last"; OnPropertyChanged(nameof(RestoreModeAsk)); }
        }

        // ══════════════════════════════════════════════════════════════
        //  INIT  (syncs checkboxes with settings on load)
        // ══════════════════════════════════════════════════════════════

        public void LoadFromSettings()
        {
            _passwordEnabled = SettingsService.Instance.PasswordProtectionEnabled;
            _enableCrashLog = SettingsService.Instance.EnableCrashLog;
            OnPropertyChanged(nameof(PasswordEnabled));
            OnPropertyChanged(nameof(IsPasswordConfigured));
            OnPropertyChanged(nameof(IsEnglish));
            OnPropertyChanged(nameof(IsRussian));
            OnPropertyChanged(nameof(RestoreModeAsk));
            OnPropertyChanged(nameof(RestoreModeLast));
            OnPropertyChanged(nameof(EnableCrashLog));
        }
    }
}