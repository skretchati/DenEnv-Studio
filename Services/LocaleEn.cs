namespace DevEnvStudio.Services
{
    /// <summary>
    /// English (en) localization strings.
    /// </summary>
    public sealed class LocaleEn
    {
        public static LocaleEn Instance { get; } = new();
        private LocaleEn() { }
        public const string Code = "en";

        // Window / Nav
        public const string AppTitle           = "DevEnv Studio";
        public const string NavDashboard       = "Dashboard";
        public const string NavBackups         = "Backups";
        public const string NavRestore         = "Restore";

        public const string NavSettings        = "Settings";
        public const string NavHelp            = "Help";
        public const string NavAbout           = "About";
        public const string EngineStatus       = "Backup Engine:";
        public const string EngineIdle         = "Idle";
        public const string EngineBusy         = "Busy";
        public const string Minimize           = "Minimize";
        public const string Maximize           = "Maximize";
        public const string Close              = "Close";

        // Dashboard
        public const string PageTitle          = "Dashboard";
        public const string PageSubtitle       = "Backup, restore and manage your developer environment.";
        public const string CreateBackup       = "Create Backup";
        public const string RestoreBackup      = "Restore Backup";
        public const string RecentBackups      = "Recent Backups";
        public const string ViewAll            = "View all";
        public const string Logs               = "Logs";
        public const string ScanStatus         = "Scan Status";
        public const string Scanning           = "SCANNING";
        public const string RestorePreview     = "Restore Preview";
        public const string OpenArchive        = "Open Archive";
        public const string NoBackupsYet       = "No backups yet";
        public const string SystemInformation  = "System Information";
        public const string BackupDestination  = "Backup Destination";
        public const string Actions            = "Actions";
        public const string ActionCreate       = "Create Backup";
        public const string ActionCreateSub    = "Start a new backup";
        public const string ActionRestore      = "Restore Backup";
        public const string ActionRestoreSub   = "Restore from an existing backup";
        public const string ActionArchive      = "Open Archive";
        public const string ActionArchiveSub   = "Browse and inspect archive";
        public const string ActionCompare      = "Compare Backups";
        public const string ActionCompareSub   = "Compare two backups";

        // Table headers
        public const string ColName            = "NAME";
        public const string ColSource          = "SOURCE";
        public const string ColDate            = "DATE";
        public const string ColSize            = "SIZE";
        public const string ColStatus          = "STATUS";
        public const string ColRestorePath     = "Restore path";
        public const string ColRestoreSize     = "Size";
        public const string ColRestoreType     = "Type";

        // Stat cards
        public const string StatBackups        = "Backups";
        public const string StatLastBackup     = "Last Backup";
        public const string StatStorageUsed    = "Storage Used";
        public const string StatFreeSpace      = "Free Space";

        // System info
        public const string SysOs              = "OS";
        public const string SysCpu             = "CPU";
        public const string SysRam             = "RAM";
        public const string SysStorage         = "Storage";
        public const string SysLocation        = "Location";
        public const string SysType            = "Type";
        public const string SysFreeSpace       = "Free Space";
        public const string SysPrimaryUsage    = "Primary Drive Usage";
        public const string SysCurrentPath     = "Current path:";

        // Status messages
        public const string StatusReady        = "Ready";
        public const string StatusScanning     = "Scanning for backups…";
        public const string StatusScanComplete = "Scan complete — {0} backup(s) found";
        public const string StatusScanNone     = "Scan complete — no backups found";
        public const string StatusScanCancel   = "Scan cancelled";
        public const string StatusCreating     = "Creating backup…";
        public const string StatusRestoring    = "Restoring {0}…";
        public const string StatusValidating   = "Validating {0}…";
        public const string StatusBackupOk     = "Backup complete — {0} files in {1}";
        public const string StatusBackupFail   = "Backup failed: {0}";
        public const string StatusRestoreOk    = "Restore complete — {0} files in {1}";
        public const string StatusRestoreFail  = "Restore failed: {0}";
        public const string StatusValidateOk   = "Archive valid: {0}";
        public const string StatusValidateFail = "Validation failed: {0}";
        public const string StatusArchiveOpen  = "Archive opened: {0} files extracted";
        public const string StatusNoArchive    = "No archive to open — run a backup scan first.";
        public const string StatusOpenCancel   = "Archive open cancelled.";
        public const string StatusOpenFail     = "Failed to open archive: {0}";
        public const string StatusCompareFail  = "Comparison failed: {0}";

        // Settings
        public const string SettingsTitle      = "Settings";
        public const string LangLabel          = "Interface Language";
        public const string LangEn             = "English";
        public const string LangRu             = "Russian (Русский)";
        public const string PasswordLabel      = "Password Protection";
        public const string PasswordDesc       = "Encrypt backups with a user-defined password (in addition to app key)";
        public const string PasswordPlaceholder = "Enter password";
        public const string PasswordConfirm    = "Confirm password";
        public const string PasswordMismatch   = "Passwords do not match.";
        public const string PasswordSave       = "Save Password";
        public const string PasswordEnable     = "Enable";
        public const string PasswordConfigured = "Password protection is configured.";
        public const string PasswordSaved      = "Password Protection — saved.";
        public const string PasswordPromptTitle = "Enter backup password";
        public const string PasswordPromptDesc  = "Enter the backup password to enable backup and restore operations.";
        public const string PasswordWrong      = "Incorrect password. Password-protected backups will be unavailable until you enter the correct password in Settings.";
        public const string RestoreModeLabel   = "Restore Mode";
        public const string RestoreModeAsk     = "Ask every time";
        public const string RestoreModeLast    = "Use latest backup";
        public const string RestoreConfirm     = "Restore the latest backup?";
        public const string RestoreTitle        = "Restore";

        // Common dialog buttons
        public const string Cancel              = "Cancel";
        public const string Ok                  = "OK";

        // File dialogs
        public const string DlgPickBackup      = "Select a DevEnvStudio Backup";
        public const string DlgPickFirst       = "Select the FIRST backup to compare";
        public const string DlgPickSecond      = "Select the SECOND backup to compare";

        // Comparison
        public const string CompareTitle       = "Backup Comparison";
        public const string CompareOnlyA       = "Only in A";
        public const string CompareOnlyB       = "Only in B";
        public const string CompareIdentical   = "Identical file sets.";
        public const string CompareSizeChanges = "Size changes";
        public const string CompareCommon      = "Common files";
        public const string CompareAndMore     = "… and {0} more";

        // Misc
        public const string Never              = "Never";
        public const string NoneFound          = "None found";
        public const string Success            = "Success";
        public const string Warning            = "Warning";
        public const string Filed              = "File";
        public const string Folder             = "Folder";
        public const string OperationRunning   = "Operation already in progress.";
        public const string Cancelling         = "Cancelling…";
        public const string InitError          = "Init error: {0}";
        public const string ScanError          = "Scan error: {0}";
        public const string SystemInfoError    = "System info error: {0}";
        public const string StorageScanError   = "Storage scan error: {0}";
        public const string UnknownCpu         = "Unknown CPU";
        public const string UnknownRam         = "Unknown";
    }
}