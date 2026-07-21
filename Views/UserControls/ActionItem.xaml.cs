using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevEnvStudio.Views.UserControls
{
    public partial class ActionItem : UserControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ActionItem),
                new PropertyMetadata("Backup", OnChanged));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ActionItem),
                new PropertyMetadata(string.Empty, OnChanged));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ActionItem),
                new PropertyMetadata(string.Empty, OnChanged));

        public string Icon     { get => (string)GetValue(IconProperty);     set => SetValue(IconProperty, value); }
        public string Title    { get => (string)GetValue(TitleProperty);    set => SetValue(TitleProperty, value); }
        public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

        // ── Routed click event for parent binding ──────────────────────
        public static readonly RoutedEvent ActionClickEvent =
            EventManager.RegisterRoutedEvent(
                nameof(ActionClick), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ActionItem));

        public event RoutedEventHandler ActionClick
        {
            add    => AddHandler(ActionClickEvent, value);
            remove => RemoveHandler(ActionClickEvent, value);
        }

        public ActionItem()
        {
            InitializeComponent();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ActionItem ai) ai.Refresh();
        }

        private void Refresh()
        {
            TbTitle.Text    = Title;
            TbSubtitle.Text = Subtitle;

            (string pathData, Color color) = Icon switch
            {
                "Backup"   => ("M20 6h-2.18c.07-.44.18-.88.18-1.36C18 2.53 15.48 1 13 1c-1.32 0-2.6.53-3.54 1.46L7 5H4c-1.11 0-2 .89-2 2v11c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2z",
                              Color.FromRgb(0x4F, 0x88, 0xF5)),
                "Restore"  => ("M12 5V1L7 6l5 5V7c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6H4c0 4.42 3.58 8 8 8s8-3.58 8-8-3.58-8-8-8z",
                              Color.FromRgb(0x22, 0xC5, 0x5E)),
                "Archive"  => ("M20 6h-8l-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2z",
                              Color.FromRgb(0xF5, 0x9E, 0x0B)),
                "Compare"  => ("M10 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h5v2h2V1h-2v2zm0 15H5l5-6v6zm9-15h-5v2h5v13l-5-6v8h5c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z",
                              Color.FromRgb(0xA8, 0x55, 0xF7)),
                "Schedule" => ("M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67V7z",
                              Color.FromRgb(0x89, 0x92, 0xAD)),
                _ => ("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z",
                      Color.FromRgb(0x4F, 0x88, 0xF5))
            };

            IconPath.Data = Geometry.Parse(pathData);
            IconPath.Fill = new SolidColorBrush(color);
        }

        /// <summary>Called when the inner Button is clicked — raises ActionClick routed event.</summary>
        private void OnButtonClick(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ActionClickEvent, this));
        }
    }
}
