using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevEnvStudio.Views.UserControls
{
    public partial class SysInfoRow : UserControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(SysInfoRow),
                new PropertyMetadata("OS", OnChanged));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(SysInfoRow),
                new PropertyMetadata(string.Empty, OnChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(SysInfoRow),
                new PropertyMetadata(string.Empty, OnChanged));

        public string Icon  { get => (string)GetValue(IconProperty);  set => SetValue(IconProperty, value); }
        public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

        public SysInfoRow()
        {
            InitializeComponent();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SysInfoRow r) r.Refresh();
        }

        private void Refresh()
        {
            TbLabel.Text = Label;
            TbValue.Text = Value;

            (string pathData, Color color) = Icon switch
            {
                "OS"  => ("M21 3H3c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H3V5h18v14z",
                          Color.FromRgb(0x4F, 0x88, 0xF5)),
                "CPU" => ("M9.825 8l-1.1-2H7V4H5v2H3v14h18V6h-2V4h-2v2h-1.725l-1.1 2H9.825zM19 18H5V8h3.275l1.1 2h5.25l1.1-2H19v10z",
                          Color.FromRgb(0xA8, 0x55, 0xF7)),
                "RAM" => ("M4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6zm16-4H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-1 9H9V9h10v2zm-4 4H9v-2h6v2zm4-8H9V5h10v2z",
                          Color.FromRgb(0x22, 0xC5, 0x5E)),
                "HDD" => ("M6 2v6l2 2-2 2v6l6-3 6 3v-6l-2-2 2-2V2l-6 3-6-3zm4 2.76L12 3.5l2 1.26v4.48L12 10.5l-2-1.26V4.76zm-2 7.04l2-1.3 2 1.3-.01 4.68L12 17.5l-1.99-1.02.01-4.68z",
                          Color.FromRgb(0xF5, 0x9E, 0x0B)),
                _     => ("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2z",
                          Color.FromRgb(0x89, 0x92, 0xAD))
            };

            IconPath.Data = Geometry.Parse(pathData);
            IconPath.Fill = new SolidColorBrush(color);
        }
    }
}
