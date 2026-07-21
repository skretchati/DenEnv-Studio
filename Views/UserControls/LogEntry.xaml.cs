using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevEnvStudio.Views.UserControls
{
    public partial class LogEntry : UserControl
    {
        public static readonly DependencyProperty TimeProperty =
            DependencyProperty.Register(nameof(Time), typeof(string), typeof(LogEntry),
                new PropertyMetadata(string.Empty, OnChanged));

        public static readonly DependencyProperty LevelProperty =
            DependencyProperty.Register(nameof(Level), typeof(string), typeof(LogEntry),
                new PropertyMetadata("INFO", OnChanged));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(nameof(Message), typeof(string), typeof(LogEntry),
                new PropertyMetadata(string.Empty, OnChanged));

        public string Time    { get => (string)GetValue(TimeProperty);    set => SetValue(TimeProperty, value); }
        public string Level   { get => (string)GetValue(LevelProperty);   set => SetValue(LevelProperty, value); }
        public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

        public LogEntry()
        {
            InitializeComponent();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogEntry le) le.Refresh();
        }

        private void Refresh()
        {
            TbTime.Text    = Time;
            TbMessage.Text = Message;
            TbMessageTooltip.Text = Message;
            TbLevel.Text   = Level;

            bool isWarn = Level?.Equals("WARN", System.StringComparison.OrdinalIgnoreCase) == true;

            if (isWarn)
            {
                LevelBadge.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x1D, 0x08));
                TbLevel.Foreground    = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
                TbMessage.Foreground  = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            else
            {
                LevelBadge.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x17, 0x2E));
                TbLevel.Foreground    = new SolidColorBrush(Color.FromRgb(0x4F, 0x88, 0xF5));
                TbMessage.Foreground  = new SolidColorBrush(Color.FromRgb(0x89, 0x92, 0xAD));
            }
        }
    }
}
