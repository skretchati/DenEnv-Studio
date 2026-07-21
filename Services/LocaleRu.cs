namespace DevEnvStudio.Services
{
    /// <summary>
    /// Russian (ru) localization strings.
    /// </summary>
    public sealed class LocaleRu
    {
        public static LocaleRu Instance { get; } = new();
        private LocaleRu() { }
        public const string Code = "ru";

        // Window / Nav
        public const string AppTitle           = "DevEnv Studio";
        public const string NavDashboard       = "Панель";
        public const string NavBackups         = "Бэкапы";
        public const string NavRestore         = "Восст.";

        public const string NavSettings        = "Настройки";
        public const string NavHelp            = "Помощь";
        public const string NavAbout           = "О программе";
        public const string EngineStatus       = "Движок бэкапа:";
        public const string EngineIdle         = "Ожидание";
        public const string EngineBusy         = "Занят";
        public const string Minimize           = "Свернуть";
        public const string Maximize           = "Развернуть";
        public const string Close              = "Закрыть";

        // Dashboard
        public const string PageTitle          = "Панель управления";
        public const string PageSubtitle       = "Бэкап, восстановление и управление средой разработки.";
        public const string CreateBackup       = "Создать бэкап";
        public const string RestoreBackup      = "Восстановить";
        public const string RecentBackups      = "Последние бэкапы";
        public const string ViewAll            = "Все";
        public const string Logs               = "Журнал";
        public const string ScanStatus         = "Статус сканирования";
        public const string Scanning           = "СКАНИРОВАНИЕ";
        public const string RestorePreview     = "Предпросмотр";
        public const string OpenArchive        = "Открыть архив";
        public const string NoBackupsYet       = "Бэкапов пока нет";
        public const string SystemInformation  = "Информация о системе";
        public const string BackupDestination  = "Место хранения";
        public const string Actions            = "Действия";
        public const string ActionCreate       = "Создать бэкап";
        public const string ActionCreateSub    = "Запустить новый бэкап";
        public const string ActionRestore      = "Восстановить";
        public const string ActionRestoreSub   = "Восстановить из бэкапа";
        public const string ActionArchive      = "Открыть архив";
        public const string ActionArchiveSub   = "Просмотреть содержимое";
        public const string ActionCompare      = "Сравнить бэкапы";
        public const string ActionCompareSub   = "Сравнить два бэкапа";

        // Table headers
        public const string ColName            = "ИМЯ";
        public const string ColSource          = "ИСТОЧНИК";
        public const string ColDate            = "ДАТА";
        public const string ColSize            = "РАЗМЕР";
        public const string ColStatus          = "СТАТУС";
        public const string ColRestorePath     = "Путь восстановления";
        public const string ColRestoreSize     = "Размер";
        public const string ColRestoreType     = "Тип";

        // Stat cards
        public const string StatBackups        = "Бэкапов";
        public const string StatLastBackup     = "Последний";
        public const string StatStorageUsed    = "Занято";
        public const string StatFreeSpace      = "Свободно";

        // System info
        public const string SysOs              = "ОС";
        public const string SysCpu             = "ЦП";
        public const string SysRam             = "ОЗУ";
        public const string SysStorage         = "Хранилище";
        public const string SysLocation        = "Расположение";
        public const string SysType            = "Тип";
        public const string SysFreeSpace       = "Свободно";
        public const string SysPrimaryUsage    = "Использование диска";
        public const string SysCurrentPath     = "Текущий путь:";

        // Status messages
        public const string StatusReady        = "Готов";
        public const string StatusScanning     = "Поиск бэкапов…";
        public const string StatusScanComplete = "Сканирование завершено — найдено {0} бэкап(ов)";
        public const string StatusScanNone     = "Сканирование завершено — бэкапы не найдены";
        public const string StatusScanCancel   = "Сканирование отменено";
        public const string StatusCreating     = "Создание бэкапа…";
        public const string StatusRestoring    = "Восстановление {0}…";
        public const string StatusValidating   = "Проверка {0}…";
        public const string StatusBackupOk     = "Бэкап создан — {0} файлов за {1}";
        public const string StatusBackupFail   = "Ошибка бэкапа: {0}";
        public const string StatusRestoreOk    = "Восстановлено — {0} файлов за {1}";
        public const string StatusRestoreFail  = "Ошибка восстановления: {0}";
        public const string StatusValidateOk   = "Архив корректен: {0}";
        public const string StatusValidateFail = "Проверка не пройдена: {0}";
        public const string StatusArchiveOpen  = "Архив открыт: извлечено {0} файлов";
        public const string StatusNoArchive    = "Нет архива для открытия — выполните сканирование.";
        public const string StatusOpenCancel   = "Открытие архива отменено.";
        public const string StatusOpenFail     = "Ошибка открытия архива: {0}";
        public const string StatusCompareFail  = "Ошибка сравнения: {0}";

        // Settings
        public const string SettingsTitle      = "Настройки";
        public const string LangLabel          = "Язык интерфейса";
        public const string LangEn             = "English";
        public const string LangRu             = "Русский";
        public const string PasswordLabel      = "Защита паролем";
        public const string PasswordDesc       = "Шифровать бэкапы пользовательским паролем (дополнительно к ключу приложения)";
        public const string PasswordPlaceholder = "Введите пароль";
        public const string PasswordConfirm    = "Подтвердите пароль";
        public const string PasswordMismatch   = "Пароли не совпадают.";
        public const string PasswordSave       = "Сохранить пароль";
        public const string PasswordEnable     = "Включить";
        public const string PasswordConfigured = "Защита паролем настроена.";
        public const string PasswordSaved      = "Защита паролем — сохранена.";
        public const string PasswordPromptTitle = "Введите пароль бэкапа";
        public const string PasswordPromptDesc  = "Введите пароль бэкапа для доступа к операциям создания и восстановления.";
        public const string PasswordWrong      = "Неверный пароль. Защищённые паролем бэкапы будут недоступны, пока вы не введёте правильный пароль в Настройках.";
        public const string RestoreModeLabel   = "Режим восстановления";
        public const string RestoreModeAsk     = "Спрашивать каждый раз";
        public const string RestoreModeLast    = "Использовать последний";
        public const string RestoreConfirm     = "Восстановить последний архив?";
        public const string RestoreTitle        = "Восстановление";

        // Common dialog buttons
        public const string Cancel              = "Отмена";
        public const string Ok                  = "ОК";

        // File dialogs
        public const string DlgPickBackup      = "Выберите бэкап DevEnvStudio";
        public const string DlgPickFirst       = "Выберите ПЕРВЫЙ бэкап для сравнения";
        public const string DlgPickSecond      = "Выберите ВТОРОЙ бэкап для сравнения";

        // Comparison
        public const string CompareTitle       = "Сравнение бэкапов";
        public const string CompareOnlyA       = "Только в A";
        public const string CompareOnlyB       = "Только в B";
        public const string CompareIdentical   = "Наборы файлов идентичны.";
        public const string CompareSizeChanges = "Изменения размера";
        public const string CompareCommon      = "Общие файлы";
        public const string CompareAndMore     = "… и ещё {0}";

        // Misc
        public const string Never              = "Никогда";
        public const string NoneFound          = "Не найдено";
        public const string Success            = "Успешно";
        public const string Warning            = "Предупр.";
        public const string Filed              = "Файл";
        public const string Folder             = "Папка";
        public const string OperationRunning   = "Операция уже выполняется.";
        public const string Cancelling         = "Отмена…";
        public const string InitError          = "Ошибка инициализации: {0}";
        public const string ScanError          = "Ошибка сканирования: {0}";
        public const string SystemInfoError    = "Ошибка информации о системе: {0}";
        public const string StorageScanError   = "Ошибка сканирования хранилища: {0}";
        public const string UnknownCpu         = "Неизвестный ЦП";
        public const string UnknownRam         = "Неизвестно";
    }
}