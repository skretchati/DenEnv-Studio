using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace DevEnvStudio.Services
{
    public enum LogLevel { INFO, WARN, ERROR }

    public sealed class LogEntry
    {
        public string Time    { get; init; } = string.Empty;
        public LogLevel Level { get; init; }
        public string Message { get; init; } = string.Empty;

        public string LevelText => Level.ToString();
    }

    public sealed class LogService
    {
        private static readonly Lazy<LogService> _instance =
            new(() => new LogService(), true);

        public static LogService Instance => _instance.Value;

        private LogService() { }

        public ObservableCollection<LogEntry> Entries { get; } = new();

        public void Log(LogLevel level, string message)
        {
            var entry = new LogEntry
            {
                Time    = DateTime.Now.ToString("HH:mm:ss"),
                Level   = level,
                Message = message
            };

            if (Application.Current?.Dispatcher is Dispatcher d && !d.CheckAccess())
                d.BeginInvoke(() => Entries.Insert(0, entry));
            else
                Entries.Insert(0, entry);
        }

        public void Info(string msg)  => Log(LogLevel.INFO,  msg);
        public void Warn(string msg)  => Log(LogLevel.WARN,  msg);
        public void Error(string msg) => Log(LogLevel.ERROR, msg);
    }
}
