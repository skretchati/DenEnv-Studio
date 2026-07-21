using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DevEnvStudio.Services
{
    /// <summary>
    /// Provides live localization strings via INotifyPropertyChanged.
    /// When the language changes, all bound UI elements update automatically.
    ///
    /// Usage in XAML:
    ///   Text="{Binding Source={x:Static svc:LocalizationService.Instance}, Path=PageTitle}"
    /// </summary>
    public sealed class LocalizationService : INotifyPropertyChanged
    {
        private static readonly Lazy<LocalizationService> _instance =
            new(() => new LocalizationService(), true);
        public static LocalizationService Instance => _instance.Value;

        private LocalizationService()
        {
            _locale = SettingsService.Instance.Language == "ru" ? LocaleRu.Instance : LocaleEn.Instance;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private object _locale;

        public string CurrentLanguage => SettingsService.Instance.Language;

        // ── Switch language ──────────────────────────────────────────

        public void SetLanguage(string code)
        {
            if (code == CurrentLanguage) return;
            SettingsService.Instance.Language = code;
            _locale = code == "ru" ? LocaleRu.Instance : LocaleEn.Instance;
            OnAllPropertiesChanged();
        }

        public void OnAllPropertiesChanged()
        {
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(string.Empty));
        }

        // ═════════════════════════════════════════════════════════════
        //  PROPERTIES  (one per localization key)
        // ═════════════════════════════════════════════════════════════

        // Window / Nav
        public string AppTitle           => Resolve(nameof(LocaleEn.AppTitle));
        public string NavDashboard       => Resolve(nameof(LocaleEn.NavDashboard));
        public string NavBackups         => Resolve(nameof(LocaleEn.NavBackups));
        public string NavRestore         => Resolve(nameof(LocaleEn.NavRestore));

        public string NavSettings        => Resolve(nameof(LocaleEn.NavSettings));
        public string NavHelp            => Resolve(nameof(LocaleEn.NavHelp));
        public string NavAbout           => Resolve(nameof(LocaleEn.NavAbout));
        public string EngineStatus       => Resolve(nameof(LocaleEn.EngineStatus));
        public string EngineIdle         => Resolve(nameof(LocaleEn.EngineIdle));
        public string EngineBusy         => Resolve(nameof(LocaleEn.EngineBusy));
        public string Minimize           => Resolve(nameof(LocaleEn.Minimize));
        public string Maximize           => Resolve(nameof(LocaleEn.Maximize));
        public string Close              => Resolve(nameof(LocaleEn.Close));

        // Dashboard
        public string PageTitle          => Resolve(nameof(LocaleEn.PageTitle));
        public string PageSubtitle       => Resolve(nameof(LocaleEn.PageSubtitle));
        public string CreateBackup       => Resolve(nameof(LocaleEn.CreateBackup));
        public string RestoreBackup      => Resolve(nameof(LocaleEn.RestoreBackup));
        public string RecentBackups      => Resolve(nameof(LocaleEn.RecentBackups));
        public string ViewAll            => Resolve(nameof(LocaleEn.ViewAll));
        public string Logs               => Resolve(nameof(LocaleEn.Logs));
        public string ScanStatus         => Resolve(nameof(LocaleEn.ScanStatus));
        public string Scanning           => Resolve(nameof(LocaleEn.Scanning));
        public string RestorePreview     => Resolve(nameof(LocaleEn.RestorePreview));
        public string OpenArchive        => Resolve(nameof(LocaleEn.OpenArchive));
        public string NoBackupsYet       => Resolve(nameof(LocaleEn.NoBackupsYet));
        public string SystemInformation  => Resolve(nameof(LocaleEn.SystemInformation));
        public string BackupDestination  => Resolve(nameof(LocaleEn.BackupDestination));
        public string Actions            => Resolve(nameof(LocaleEn.Actions));

        public string ActionCreate       => Resolve(nameof(LocaleEn.ActionCreate));
        public string ActionCreateSub    => Resolve(nameof(LocaleEn.ActionCreateSub));
        public string ActionRestore      => Resolve(nameof(LocaleEn.ActionRestore));
        public string ActionRestoreSub   => Resolve(nameof(LocaleEn.ActionRestoreSub));
        public string ActionArchive      => Resolve(nameof(LocaleEn.ActionArchive));
        public string ActionArchiveSub   => Resolve(nameof(LocaleEn.ActionArchiveSub));
        public string ActionCompare      => Resolve(nameof(LocaleEn.ActionCompare));
        public string ActionCompareSub   => Resolve(nameof(LocaleEn.ActionCompareSub));

        // Table headers
        public string ColName            => Resolve(nameof(LocaleEn.ColName));
        public string ColSource          => Resolve(nameof(LocaleEn.ColSource));
        public string ColDate            => Resolve(nameof(LocaleEn.ColDate));
        public string ColSize            => Resolve(nameof(LocaleEn.ColSize));
        public string ColStatus          => Resolve(nameof(LocaleEn.ColStatus));
        public string ColRestorePath     => Resolve(nameof(LocaleEn.ColRestorePath));
        public string ColRestoreSize     => Resolve(nameof(LocaleEn.ColRestoreSize));
        public string ColRestoreType     => Resolve(nameof(LocaleEn.ColRestoreType));

        // Stat cards
        public string StatBackups        => Resolve(nameof(LocaleEn.StatBackups));
        public string StatLastBackup     => Resolve(nameof(LocaleEn.StatLastBackup));
        public string StatStorageUsed    => Resolve(nameof(LocaleEn.StatStorageUsed));
        public string StatFreeSpace      => Resolve(nameof(LocaleEn.StatFreeSpace));

        // System info
        public string SysOs              => Resolve(nameof(LocaleEn.SysOs));
        public string SysCpu             => Resolve(nameof(LocaleEn.SysCpu));
        public string SysRam             => Resolve(nameof(LocaleEn.SysRam));
        public string SysStorage         => Resolve(nameof(LocaleEn.SysStorage));
        public string SysLocation        => Resolve(nameof(LocaleEn.SysLocation));
        public string SysType            => Resolve(nameof(LocaleEn.SysType));
        public string SysFreeSpace       => Resolve(nameof(LocaleEn.SysFreeSpace));
        public string SysPrimaryUsage    => Resolve(nameof(LocaleEn.SysPrimaryUsage));
        public string SysCurrentPath     => Resolve(nameof(LocaleEn.SysCurrentPath));

        // Settings
        public string SettingsTitle      => Resolve(nameof(LocaleEn.SettingsTitle));
        public string LangLabel          => Resolve(nameof(LocaleEn.LangLabel));
        public string LangEn             => Resolve(nameof(LocaleEn.LangEn));
        public string LangRu             => Resolve(nameof(LocaleEn.LangRu));
        public string PasswordLabel      => Resolve(nameof(LocaleEn.PasswordLabel));
        public string PasswordDesc       => Resolve(nameof(LocaleEn.PasswordDesc));
        public string PasswordPlaceholder => Resolve(nameof(LocaleEn.PasswordPlaceholder));
        public string PasswordConfirm    => Resolve(nameof(LocaleEn.PasswordConfirm));
        public string PasswordMismatch   => Resolve(nameof(LocaleEn.PasswordMismatch));
        public string RestoreModeLabel   => Resolve(nameof(LocaleEn.RestoreModeLabel));
        public string RestoreModeAsk     => Resolve(nameof(LocaleEn.RestoreModeAsk));
        public string RestoreModeLast    => Resolve(nameof(LocaleEn.RestoreModeLast));
        public string PasswordSave       => Resolve(nameof(LocaleEn.PasswordSave));
        public string PasswordEnable     => Resolve(nameof(LocaleEn.PasswordEnable));
        public string PasswordConfigured => Resolve(nameof(LocaleEn.PasswordConfigured));
        public string PasswordSaved      => Resolve(nameof(LocaleEn.PasswordSaved));
        public string PasswordPromptTitle => Resolve(nameof(LocaleEn.PasswordPromptTitle));
        public string PasswordPromptDesc  => Resolve(nameof(LocaleEn.PasswordPromptDesc));
        public string PasswordWrong      => Resolve(nameof(LocaleEn.PasswordWrong));
        public string RestoreConfirm     => Resolve(nameof(LocaleEn.RestoreConfirm));
        public string RestoreTitle        => Resolve(nameof(LocaleEn.RestoreTitle));
        public string Cancel              => Resolve(nameof(LocaleEn.Cancel));
        public string Ok                  => Resolve(nameof(LocaleEn.Ok));
        public string InitError           => Resolve(nameof(LocaleEn.InitError));

        private string Resolve(string key)
        {
            if (_locale is LocaleEn)
            {
                return key switch
                {
                    nameof(LocaleEn.AppTitle)           => LocaleEn.AppTitle,
                    nameof(LocaleEn.NavDashboard)       => LocaleEn.NavDashboard,
                    nameof(LocaleEn.NavBackups)         => LocaleEn.NavBackups,
                    nameof(LocaleEn.NavRestore)         => LocaleEn.NavRestore,
                    nameof(LocaleEn.NavSettings)        => LocaleEn.NavSettings,
                    nameof(LocaleEn.NavHelp)            => LocaleEn.NavHelp,
                    nameof(LocaleEn.NavAbout)           => LocaleEn.NavAbout,
                    nameof(LocaleEn.EngineStatus)       => LocaleEn.EngineStatus,
                    nameof(LocaleEn.EngineIdle)         => LocaleEn.EngineIdle,
                    nameof(LocaleEn.EngineBusy)         => LocaleEn.EngineBusy,
                    nameof(LocaleEn.Minimize)           => LocaleEn.Minimize,
                    nameof(LocaleEn.Maximize)           => LocaleEn.Maximize,
                    nameof(LocaleEn.Close)              => LocaleEn.Close,
                    nameof(LocaleEn.PageTitle)          => LocaleEn.PageTitle,
                    nameof(LocaleEn.PageSubtitle)       => LocaleEn.PageSubtitle,
                    nameof(LocaleEn.CreateBackup)       => LocaleEn.CreateBackup,
                    nameof(LocaleEn.RestoreBackup)      => LocaleEn.RestoreBackup,
                    nameof(LocaleEn.RecentBackups)      => LocaleEn.RecentBackups,
                    nameof(LocaleEn.ViewAll)            => LocaleEn.ViewAll,
                    nameof(LocaleEn.Logs)               => LocaleEn.Logs,
                    nameof(LocaleEn.ScanStatus)         => LocaleEn.ScanStatus,
                    nameof(LocaleEn.Scanning)           => LocaleEn.Scanning,
                    nameof(LocaleEn.RestorePreview)     => LocaleEn.RestorePreview,
                    nameof(LocaleEn.OpenArchive)        => LocaleEn.OpenArchive,
                    nameof(LocaleEn.NoBackupsYet)       => LocaleEn.NoBackupsYet,
                    nameof(LocaleEn.SystemInformation)  => LocaleEn.SystemInformation,
                    nameof(LocaleEn.BackupDestination)  => LocaleEn.BackupDestination,
                    nameof(LocaleEn.Actions)            => LocaleEn.Actions,
                    nameof(LocaleEn.ActionCreate)       => LocaleEn.ActionCreate,
                    nameof(LocaleEn.ActionCreateSub)    => LocaleEn.ActionCreateSub,
                    nameof(LocaleEn.ActionRestore)      => LocaleEn.ActionRestore,
                    nameof(LocaleEn.ActionRestoreSub)   => LocaleEn.ActionRestoreSub,
                    nameof(LocaleEn.ActionArchive)      => LocaleEn.ActionArchive,
                    nameof(LocaleEn.ActionArchiveSub)   => LocaleEn.ActionArchiveSub,
                    nameof(LocaleEn.ActionCompare)      => LocaleEn.ActionCompare,
                    nameof(LocaleEn.ActionCompareSub)   => LocaleEn.ActionCompareSub,
                    nameof(LocaleEn.ColName)            => LocaleEn.ColName,
                    nameof(LocaleEn.ColSource)          => LocaleEn.ColSource,
                    nameof(LocaleEn.ColDate)            => LocaleEn.ColDate,
                    nameof(LocaleEn.ColSize)            => LocaleEn.ColSize,
                    nameof(LocaleEn.ColStatus)          => LocaleEn.ColStatus,
                    nameof(LocaleEn.ColRestorePath)     => LocaleEn.ColRestorePath,
                    nameof(LocaleEn.ColRestoreSize)     => LocaleEn.ColRestoreSize,
                    nameof(LocaleEn.ColRestoreType)     => LocaleEn.ColRestoreType,
                    nameof(LocaleEn.StatBackups)        => LocaleEn.StatBackups,
                    nameof(LocaleEn.StatLastBackup)     => LocaleEn.StatLastBackup,
                    nameof(LocaleEn.StatStorageUsed)    => LocaleEn.StatStorageUsed,
                    nameof(LocaleEn.StatFreeSpace)      => LocaleEn.StatFreeSpace,
                    nameof(LocaleEn.SysOs)              => LocaleEn.SysOs,
                    nameof(LocaleEn.SysCpu)             => LocaleEn.SysCpu,
                    nameof(LocaleEn.SysRam)             => LocaleEn.SysRam,
                    nameof(LocaleEn.SysStorage)         => LocaleEn.SysStorage,
                    nameof(LocaleEn.SysLocation)        => LocaleEn.SysLocation,
                    nameof(LocaleEn.SysType)            => LocaleEn.SysType,
                    nameof(LocaleEn.SysFreeSpace)       => LocaleEn.SysFreeSpace,
                    nameof(LocaleEn.SysPrimaryUsage)    => LocaleEn.SysPrimaryUsage,
                    nameof(LocaleEn.SysCurrentPath)     => LocaleEn.SysCurrentPath,
                    nameof(LocaleEn.SettingsTitle)      => LocaleEn.SettingsTitle,
                    nameof(LocaleEn.LangLabel)          => LocaleEn.LangLabel,
                    nameof(LocaleEn.LangEn)             => LocaleEn.LangEn,
                    nameof(LocaleEn.LangRu)             => LocaleEn.LangRu,
                    nameof(LocaleEn.PasswordLabel)      => LocaleEn.PasswordLabel,
                    nameof(LocaleEn.PasswordDesc)       => LocaleEn.PasswordDesc,
                    nameof(LocaleEn.PasswordPlaceholder) => LocaleEn.PasswordPlaceholder,
                    nameof(LocaleEn.PasswordConfirm)    => LocaleEn.PasswordConfirm,
                    nameof(LocaleEn.PasswordMismatch)   => LocaleEn.PasswordMismatch,
                    nameof(LocaleEn.RestoreModeLabel)   => LocaleEn.RestoreModeLabel,
                    nameof(LocaleEn.RestoreModeAsk)     => LocaleEn.RestoreModeAsk,
                    nameof(LocaleEn.RestoreModeLast)    => LocaleEn.RestoreModeLast,
                    nameof(LocaleEn.PasswordSave)       => LocaleEn.PasswordSave,
                    nameof(LocaleEn.PasswordEnable)     => LocaleEn.PasswordEnable,
                    nameof(LocaleEn.PasswordConfigured) => LocaleEn.PasswordConfigured,
                    nameof(LocaleEn.PasswordSaved)      => LocaleEn.PasswordSaved,
                    nameof(LocaleEn.PasswordPromptTitle) => LocaleEn.PasswordPromptTitle,
                    nameof(LocaleEn.PasswordPromptDesc)  => LocaleEn.PasswordPromptDesc,
                    nameof(LocaleEn.PasswordWrong)      => LocaleEn.PasswordWrong,
                    nameof(LocaleEn.RestoreConfirm)     => LocaleEn.RestoreConfirm,
                    nameof(LocaleEn.RestoreTitle)        => LocaleEn.RestoreTitle,
                    nameof(LocaleEn.Cancel)              => LocaleEn.Cancel,
                    nameof(LocaleEn.Ok)                  => LocaleEn.Ok,

                    _ => $"{{{key}}}"
                };
            }

            if (_locale is LocaleRu)
            {
                return key switch
                {
                    nameof(LocaleRu.AppTitle)           => LocaleRu.AppTitle,
                    nameof(LocaleRu.NavDashboard)       => LocaleRu.NavDashboard,
                    nameof(LocaleRu.NavBackups)         => LocaleRu.NavBackups,
                    nameof(LocaleRu.NavRestore)         => LocaleRu.NavRestore,
                    nameof(LocaleRu.NavSettings)        => LocaleRu.NavSettings,
                    nameof(LocaleRu.NavHelp)            => LocaleRu.NavHelp,
                    nameof(LocaleRu.NavAbout)           => LocaleRu.NavAbout,
                    nameof(LocaleRu.EngineStatus)       => LocaleRu.EngineStatus,
                    nameof(LocaleRu.EngineIdle)         => LocaleRu.EngineIdle,
                    nameof(LocaleRu.EngineBusy)         => LocaleRu.EngineBusy,
                    nameof(LocaleRu.Minimize)           => LocaleRu.Minimize,
                    nameof(LocaleRu.Maximize)           => LocaleRu.Maximize,
                    nameof(LocaleRu.Close)              => LocaleRu.Close,
                    nameof(LocaleRu.PageTitle)          => LocaleRu.PageTitle,
                    nameof(LocaleRu.PageSubtitle)       => LocaleRu.PageSubtitle,
                    nameof(LocaleRu.CreateBackup)       => LocaleRu.CreateBackup,
                    nameof(LocaleRu.RestoreBackup)      => LocaleRu.RestoreBackup,
                    nameof(LocaleRu.RecentBackups)      => LocaleRu.RecentBackups,
                    nameof(LocaleRu.ViewAll)            => LocaleRu.ViewAll,
                    nameof(LocaleRu.Logs)               => LocaleRu.Logs,
                    nameof(LocaleRu.ScanStatus)         => LocaleRu.ScanStatus,
                    nameof(LocaleRu.Scanning)           => LocaleRu.Scanning,
                    nameof(LocaleRu.RestorePreview)     => LocaleRu.RestorePreview,
                    nameof(LocaleRu.OpenArchive)        => LocaleRu.OpenArchive,
                    nameof(LocaleRu.NoBackupsYet)       => LocaleRu.NoBackupsYet,
                    nameof(LocaleRu.SystemInformation)  => LocaleRu.SystemInformation,
                    nameof(LocaleRu.BackupDestination)  => LocaleRu.BackupDestination,
                    nameof(LocaleRu.Actions)            => LocaleRu.Actions,
                    nameof(LocaleRu.ActionCreate)       => LocaleRu.ActionCreate,
                    nameof(LocaleRu.ActionCreateSub)    => LocaleRu.ActionCreateSub,
                    nameof(LocaleRu.ActionRestore)      => LocaleRu.ActionRestore,
                    nameof(LocaleRu.ActionRestoreSub)   => LocaleRu.ActionRestoreSub,
                    nameof(LocaleRu.ActionArchive)      => LocaleRu.ActionArchive,
                    nameof(LocaleRu.ActionArchiveSub)   => LocaleRu.ActionArchiveSub,
                    nameof(LocaleRu.ActionCompare)      => LocaleRu.ActionCompare,
                    nameof(LocaleRu.ActionCompareSub)   => LocaleRu.ActionCompareSub,
                    nameof(LocaleRu.ColName)            => LocaleRu.ColName,
                    nameof(LocaleRu.ColSource)          => LocaleRu.ColSource,
                    nameof(LocaleRu.ColDate)            => LocaleRu.ColDate,
                    nameof(LocaleRu.ColSize)            => LocaleRu.ColSize,
                    nameof(LocaleRu.ColStatus)          => LocaleRu.ColStatus,
                    nameof(LocaleRu.ColRestorePath)     => LocaleRu.ColRestorePath,
                    nameof(LocaleRu.ColRestoreSize)     => LocaleRu.ColRestoreSize,
                    nameof(LocaleRu.ColRestoreType)     => LocaleRu.ColRestoreType,
                    nameof(LocaleRu.StatBackups)        => LocaleRu.StatBackups,
                    nameof(LocaleRu.StatLastBackup)     => LocaleRu.StatLastBackup,
                    nameof(LocaleRu.StatStorageUsed)    => LocaleRu.StatStorageUsed,
                    nameof(LocaleRu.StatFreeSpace)      => LocaleRu.StatFreeSpace,
                    nameof(LocaleRu.SysOs)              => LocaleRu.SysOs,
                    nameof(LocaleRu.SysCpu)             => LocaleRu.SysCpu,
                    nameof(LocaleRu.SysRam)             => LocaleRu.SysRam,
                    nameof(LocaleRu.SysStorage)         => LocaleRu.SysStorage,
                    nameof(LocaleRu.SysLocation)        => LocaleRu.SysLocation,
                    nameof(LocaleRu.SysType)            => LocaleRu.SysType,
                    nameof(LocaleRu.SysFreeSpace)       => LocaleRu.SysFreeSpace,
                    nameof(LocaleRu.SysPrimaryUsage)    => LocaleRu.SysPrimaryUsage,
                    nameof(LocaleRu.SysCurrentPath)     => LocaleRu.SysCurrentPath,
                    nameof(LocaleRu.SettingsTitle)      => LocaleRu.SettingsTitle,
                    nameof(LocaleRu.LangLabel)          => LocaleRu.LangLabel,
                    nameof(LocaleRu.LangEn)             => LocaleRu.LangEn,
                    nameof(LocaleRu.LangRu)             => LocaleRu.LangRu,
                    nameof(LocaleRu.PasswordLabel)      => LocaleRu.PasswordLabel,
                    nameof(LocaleRu.PasswordDesc)       => LocaleRu.PasswordDesc,
                    nameof(LocaleRu.PasswordPlaceholder) => LocaleRu.PasswordPlaceholder,
                    nameof(LocaleRu.PasswordConfirm)    => LocaleRu.PasswordConfirm,
                    nameof(LocaleRu.PasswordMismatch)   => LocaleRu.PasswordMismatch,
                    nameof(LocaleRu.RestoreModeLabel)   => LocaleRu.RestoreModeLabel,
                    nameof(LocaleRu.RestoreModeAsk)     => LocaleRu.RestoreModeAsk,
                    nameof(LocaleRu.RestoreModeLast)    => LocaleRu.RestoreModeLast,
                    nameof(LocaleRu.PasswordSave)       => LocaleRu.PasswordSave,
                    nameof(LocaleRu.PasswordEnable)     => LocaleRu.PasswordEnable,
                    nameof(LocaleRu.PasswordConfigured) => LocaleRu.PasswordConfigured,
                    nameof(LocaleRu.PasswordSaved)      => LocaleRu.PasswordSaved,
                    nameof(LocaleRu.PasswordPromptTitle) => LocaleRu.PasswordPromptTitle,
                    nameof(LocaleRu.PasswordPromptDesc)  => LocaleRu.PasswordPromptDesc,
                    nameof(LocaleRu.PasswordWrong)      => LocaleRu.PasswordWrong,
                    nameof(LocaleRu.RestoreConfirm)     => LocaleRu.RestoreConfirm,
                    nameof(LocaleRu.RestoreTitle)        => LocaleRu.RestoreTitle,
                    nameof(LocaleRu.Cancel)              => LocaleRu.Cancel,
                    nameof(LocaleRu.Ok)                  => LocaleRu.Ok,

                    _ => $"{{{key}}}"
                };
            }

            return $"{{{key}}}";
        }
    }
}