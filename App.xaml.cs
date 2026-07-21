using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Diagnostics;
using System.Windows.Media;

namespace DevEnvStudio
{
    public partial class App : Application
    {
        private static readonly string LogPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "devenv_crash_diag.log");

        private static StreamWriter? _log;

        protected override void OnStartup(StartupEventArgs e)
        {
            OpenLogFile();
            _log!.WriteLine($"[{Now()}] ── Diagnostic session started ──");

            // 1. First-chance: fires before any handler, catches the exact throw site
            AppDomain.CurrentDomain.FirstChanceException += OnFirstChance;

            // 2. Unhandled exceptions on non-UI threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandled;

            // 3. Unobserved Task exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTask;

            // 4. Unhandled exceptions on the WPF Dispatcher thread
            DispatcherUnhandledException += OnDispatcherUnhandled;

            // 5. WPF binding warning trace → log file
            PresentationTraceSources.Refresh();
            PresentationTraceSources.DataBindingSource.Listeners
                .Add(new FileTraceListener(_log));
            PresentationTraceSources.DataBindingSource.Switch.Level =
                SourceLevels.Warning;

            base.OnStartup(e);
        }

        /// <summary>
        /// Called after settings are loaded. If the user has disabled crash logging,
        /// close the file to prevent unnecessary I/O during normal operation.
        /// </summary>
        public void ApplyCrashLogSetting()
        {
            bool enabled = Services.SettingsService.Instance.EnableCrashLog;
            if (enabled && _log == null)
                OpenLogFile();
            else if (!enabled && _log != null)
                CloseLogFile();
        }

        /// <summary>
        /// Ensures the log file is open before writing a crash record.
        /// Called at the top of every crash handler.
        /// </summary>
        private static void EnsureCrashLog()
        {
            if (_log != null) return;
            OpenLogFile();
        }

        private static void OpenLogFile()
        {
            _log = new StreamWriter(LogPath, append: true, Encoding.UTF8)
                { AutoFlush = true };
        }

        private static void CloseLogFile()
        {
            try
            {
                _log?.WriteLine($"[{Now()}] Diagnostic log disabled — closing.");
                _log?.Flush();
                _log?.Dispose();
            }
            catch { /* best-effort */ }
            _log = null;
        }

        // ── First-chance: fires synchronously on the throwing thread ────────
        private static void OnFirstChance(
            object? sender, FirstChanceExceptionEventArgs args)
        {
            var ex = args.Exception;
            if (ex is not InvalidOperationException) return;
            if (!ex.Message.Contains("UnsetValue") &&
                !ex.Message.Contains("Background"))
                return;

            var sb = new StringBuilder();
            sb.AppendLine($"\n[{Now()}] ══ FIRST-CHANCE InvalidOperationException ══");
            sb.AppendLine($"  Thread  : {Thread.CurrentThread.ManagedThreadId} " +
                          $"({Thread.CurrentThread.Name ?? "unnamed"})");
            sb.AppendLine($"  Message : {ex.Message}");

            // Raw StackTrace from the throw site (most precise location)
            var raw = new StackTrace(ex, fNeedFileInfo: true);
            sb.AppendLine("  StackTrace (throw site):");
            foreach (var frame in raw.GetFrames().Take(30))
            {
                var m = frame.GetMethod();
                if (m == null) continue;
                string file  = frame.GetFileName() ?? "";
                int    line  = frame.GetFileLineNumber();
                string loc   = file.Length > 0 ? $" [{Path.GetFileName(file)}:{line}]" : "";
                sb.AppendLine($"    {m.DeclaringType?.Name}.{m.Name}{loc}");
            }

            // Capture a second stack trace from the current call stack
            // This shows the WPF render pipeline frames even if ex was re-thrown
            sb.AppendLine("  CallStack (current thread):");
            var cur = new StackTrace(fNeedFileInfo: true);
            foreach (var frame in cur.GetFrames().Take(20))
            {
                var m = frame.GetMethod();
                if (m == null) continue;
                sb.AppendLine($"    {m.DeclaringType?.FullName}.{m.Name}");
            }

            // Schedule visual tree scan on UI thread (may not have fired yet)
            Application.Current?.Dispatcher?.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                new Action(() => ScanAndLog(sb)));

            _log?.WriteLine(sb.ToString());
        }

        // ── Dispatcher unhandled: last chance before app dies ────────────────
        private void OnDispatcherUnhandled(
            object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
        {
            EnsureCrashLog();
            var sb = new StringBuilder();
            sb.AppendLine($"\n[{Now()}] ══ UNHANDLED DISPATCHER EXCEPTION ══");
            AppendException(args.Exception, sb, 0);

            sb.AppendLine("\n  Visual tree scan at crash time:");
            ScanVisualTree(sb);

            _log?.WriteLine(sb.ToString());
            _log?.Flush();

            // Do not handle — let it crash so the user sees the normal crash dialog
            args.Handled = false;
        }

        // ── Non-UI thread unhandled ──────────────────────────────────────────
        private static void OnUnhandled(object sender, UnhandledExceptionEventArgs args)
        {
            EnsureCrashLog();
            _log?.WriteLine($"\n[{Now()}] ══ UNHANDLED NON-UI EXCEPTION ══");
            if (args.ExceptionObject is Exception ex)
                AppendException(ex, new StringBuilder(), 0);
            _log?.Flush();
        }

        // ── Task exceptions ──────────────────────────────────────────────────
        private static void OnUnobservedTask(
            object? sender, UnobservedTaskExceptionEventArgs args)
        {
            EnsureCrashLog();
            _log?.WriteLine($"\n[{Now()}] ══ UNOBSERVED TASK EXCEPTION ══");
            foreach (var ex in args.Exception.InnerExceptions)
                _log?.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
        }

        // ── Visual tree scanner ──────────────────────────────────────────────
        private static void ScanAndLog(StringBuilder sb)
        {
            ScanVisualTree(sb);
            _log?.WriteLine(sb.ToString());
        }

        private static void ScanVisualTree(StringBuilder sb)
        {
            try
            {
                var win = Application.Current?.MainWindow;
                if (win == null) { sb.AppendLine("  [no MainWindow]"); return; }
                WalkVisual(win, sb, 0);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  [scan error: {ex.Message}]");
            }
        }

        private static void WalkVisual(DependencyObject obj, StringBuilder sb, int depth)
        {
            if (depth > 50) return;

            if (obj is System.Windows.Controls.Border border)
            {
                // Read the local value (what is directly set on this element)
                object local = border.ReadLocalValue(
                    System.Windows.Controls.Border.BackgroundProperty);

                // Read the computed effective value
                object effective;
                try { effective = border.GetValue(
                    System.Windows.Controls.Border.BackgroundProperty); }
                catch (Exception ex) { effective = $"[THREW: {ex.Message}]"; }

                bool suspicious =
                    local     == DependencyProperty.UnsetValue ||
                    effective == DependencyProperty.UnsetValue ||
                    effective == null;

                if (suspicious)
                {
                    string feName  = border is FrameworkElement fe ? fe.Name : "";
                    string vis     = border.Visibility.ToString();
                    string dcType  = border.DataContext?.GetType().Name ?? "null";

                    sb.AppendLine($"\n  *** SUSPECT Border (depth={depth})");
                    sb.AppendLine($"      x:Name        = '{feName}'");
                    sb.AppendLine($"      Visibility    = {vis}");
                    sb.AppendLine($"      DataContext   = {dcType}");
                    sb.AppendLine($"      LocalValue    = {FormatObj(local)}");
                    sb.AppendLine($"      EffectiveVal  = {FormatObj(effective)}");

                    // Parent chain
                    sb.Append("      Parents       =");
                    var p = VisualTreeHelper.GetParent(border);
                    int pc = 0;
                    while (p != null && pc < 10)
                    {
                        string pname = p is FrameworkElement pfe && pfe.Name.Length > 0
                            ? $"{p.GetType().Name}[{pfe.Name}]"
                            : p.GetType().Name;
                        sb.Append($" → {pname}");
                        p = VisualTreeHelper.GetParent(p);
                        pc++;
                    }
                    sb.AppendLine();

                    // Style info
                    if (border.Style is { } style)
                    {
                        sb.AppendLine($"      Style.Setters = {style.Setters.Count}");
                        foreach (var setter in style.Setters.OfType<Setter>())
                            sb.AppendLine(
                                $"        [{setter.Property?.Name}] = {FormatObj(setter.Value)}");
                        sb.AppendLine($"      Style.Triggers= {style.Triggers.Count}");
                    }
                    else
                    {
                        sb.AppendLine("      Style         = null");
                    }
                }
            }

            int n = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < n; i++)
                WalkVisual(VisualTreeHelper.GetChild(obj, i), sb, depth + 1);
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static void AppendException(Exception ex, StringBuilder sb, int depth)
        {
            string pad = new(' ', depth * 2);
            _log?.WriteLine($"{pad}Type    : {ex.GetType().FullName}");
            _log?.WriteLine($"{pad}Message : {ex.Message}");
            _log?.WriteLine($"{pad}Stack   :");
            foreach (var line in (ex.StackTrace ?? "").Split('\n').Take(30))
                _log?.WriteLine($"{pad}  {line.TrimEnd()}");
            if (ex.InnerException != null)
            {
                _log?.WriteLine($"{pad}InnerException ↓");
                AppendException(ex.InnerException, sb, depth + 1);
            }
        }

        private static string FormatObj(object? o)
        {
            if (o == null)                             return "null";
            if (o == DependencyProperty.UnsetValue)    return "*** DependencyProperty.UnsetValue ***";
            return o.ToString() ?? o.GetType().Name;
        }

        private static string Now() => DateTime.Now.ToString("HH:mm:ss.fff");
    }

    // ── Binding trace listener ────────────────────────────────────────────────
    internal sealed class FileTraceListener : TraceListener
    {
        private readonly StreamWriter _w;
        public FileTraceListener(StreamWriter w) { _w = w; }
        public override void Write(string? m)   { if (m != null) _w.Write($"[BIND] {m}"); }
        public override void WriteLine(string? m){ if (m != null) _w.WriteLine($"[BIND] {m}"); }
    }
}
