using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using DevEnvStudio.ViewModels;

namespace DevEnvStudio.Views.UserControls
{
    public partial class BackupTableRow : UserControl
    {
        public static readonly DependencyProperty BackupNameProperty =
            DependencyProperty.Register(nameof(BackupName), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata(string.Empty, OnPropsChanged));

        public static readonly DependencyProperty EnvironmentProperty =
            DependencyProperty.Register(nameof(Environment), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata(string.Empty, OnPropsChanged));

        public static readonly DependencyProperty DateProperty =
            DependencyProperty.Register(nameof(Date), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata(string.Empty, OnPropsChanged));

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(nameof(Size), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata(string.Empty, OnPropsChanged));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(nameof(Status), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata("Success", OnPropsChanged));

        public static readonly DependencyProperty IconTypeProperty =
            DependencyProperty.Register(nameof(IconType), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata("Desktop", OnPropsChanged));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(BackupTableRow),
                new PropertyMetadata(string.Empty, OnPropsChanged));

        public string BackupName  { get => (string)GetValue(BackupNameProperty);  set => SetValue(BackupNameProperty, value); }
        public string Environment { get => (string)GetValue(EnvironmentProperty); set => SetValue(EnvironmentProperty, value); }
        public string Date        { get => (string)GetValue(DateProperty);        set => SetValue(DateProperty, value); }
        public string Size        { get => (string)GetValue(SizeProperty);        set => SetValue(SizeProperty, value); }
        public string Status      { get => (string)GetValue(StatusProperty);      set => SetValue(StatusProperty, value); }
        public string IconType    { get => (string)GetValue(IconTypeProperty);    set => SetValue(IconTypeProperty, value); }
        public string Subtitle    { get => (string)GetValue(SubtitleProperty);    set => SetValue(SubtitleProperty, value); }

        // ── Click handler: tells DashboardViewModel which backup is selected ──
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Walk up to the ItemsControl to find this item's index
            DependencyObject? current = this;
            while (current != null)
            {
                if (current is ItemsControl itemsControl)
                {
                    var container = itemsControl.ItemContainerGenerator.ContainerFromItem(DataContext);
                    if (container != null)
                    {
                        int idx = itemsControl.ItemContainerGenerator.IndexFromContainer(container);
                        if (idx >= 0)
                            DashboardViewModel.Instance.SelectedBackupIndex = idx;
                    }
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        public BackupTableRow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DashboardViewModel.Instance.PropertyChanged += OnViewModelPropertyChanged;
            ApplySelectionStyle();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DashboardViewModel.Instance.PropertyChanged -= OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DashboardViewModel.SelectedBackupIndex))
                ApplySelectionStyle();
        }

        private void ApplySelectionStyle()
        {
            // Find the root Border child.
            Border? rootBorder = null;
            int childCount = VisualTreeHelper.GetChildrenCount(this);
            for (int i = 0; i < childCount; i++)
            {
                if (VisualTreeHelper.GetChild(this, i) is Border childBorder)
                {
                    rootBorder = childBorder;
                    break;
                }
            }

            if (rootBorder == null) return;

            if (DataContext is Services.BackupItem item)
            {
                int idx = DashboardViewModel.Instance.Backups.IndexOf(item);
                bool isSelected = idx >= 0 && idx == DashboardViewModel.Instance.SelectedBackupIndex;

                if (isSelected)
                {
                    rootBorder.Background = Application.Current.TryFindResource("Brush.Nav.Active.Bg") as Brush ?? Brushes.Transparent;
                    rootBorder.BorderBrush = Application.Current.TryFindResource("Brush.Nav.Active.Indicator") as Brush ?? Brushes.Transparent;
                    rootBorder.BorderThickness = new Thickness(1);
                    rootBorder.CornerRadius = new CornerRadius(4);
                }
                else
                {
                    rootBorder.Background = Brushes.Transparent;
                    rootBorder.BorderBrush = Application.Current.TryFindResource("Brush.Separator") as Brush ?? Brushes.Transparent;
                    rootBorder.BorderThickness = new Thickness(0, 0, 0, 1);
                    rootBorder.CornerRadius = new CornerRadius(0);
                }
            }
        }

        private static void OnPropsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BackupTableRow row)
                row.Refresh();
        }

        private void Refresh()
        {
            TbName.Text        = BackupName;
            TbSubtitle.Text    = Subtitle;
            TbEnvironment.Text = Environment;
            TbDate.Text        = Date;
            TbSize.Text        = Size;

            IconPath.Data = Geometry.Parse(IconType switch
            {
                "Config"   => "M12 1L3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm-2 16l-4-4 1.41-1.41L10 14.17l6.59-6.59L18 9l-8 8z",
                "Tools"    => "M22.7 19l-9.1-9.1c.9-2.3.4-5-1.5-6.9-2-2-5-2.4-7.4-1.3L9 6 6 9 1.6 4.7C.4 7.1.9 10.1 2.9 12.1c1.9 1.9 4.6 2.4 6.9 1.5l9.1 9.1c.4.4 1 .4 1.4 0l2.3-2.3c.5-.4.5-1.1.1-1.4z",
                "Archive"  => "M20 6h-8l-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2z",
                "Registry" => "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.56-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.22-.07.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z",
                _          => "M21 3H3c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z"
            });

            Color iconColor = IconType switch
            {
                "Config"  => Color.FromRgb(0x4F, 0x88, 0xF5),
                "Tools"   => Color.FromRgb(0xA8, 0x55, 0xF7),
                "Archive" => Color.FromRgb(0xF5, 0x9E, 0x0B),
                _         => Color.FromRgb(0x89, 0x92, 0xAD)
            };
            IconPath.Fill = new SolidColorBrush(iconColor);

            bool isWarning = Status?.Equals("Warning", System.StringComparison.OrdinalIgnoreCase) == true;
            Color statusColor = isWarning
                ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                : Color.FromRgb(0x22, 0xC5, 0x5E);

            TbStatus.Text       = Status;
            TbStatus.Foreground = new SolidColorBrush(statusColor);
            StatusDot.Fill      = new SolidColorBrush(statusColor);
        }
    }
}