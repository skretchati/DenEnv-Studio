using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DevEnvStudio.Views.UserControls
{
    public partial class ArchiveFileRow : UserControl
    {
        public static readonly DependencyProperty RestorePathProperty =
            DependencyProperty.Register(nameof(RestorePath), typeof(string), typeof(ArchiveFileRow),
                new PropertyMetadata(string.Empty, OnChanged));

        public static readonly DependencyProperty FileSizeProperty =
            DependencyProperty.Register(nameof(FileSize), typeof(string), typeof(ArchiveFileRow),
                new PropertyMetadata(string.Empty, OnChanged));

        public static readonly DependencyProperty FileTypeProperty =
            DependencyProperty.Register(nameof(FileType), typeof(string), typeof(ArchiveFileRow),
                new PropertyMetadata(string.Empty, OnChanged));

        public static readonly DependencyProperty IsFolderProperty =
            DependencyProperty.Register(nameof(IsFolder), typeof(bool), typeof(ArchiveFileRow),
                new PropertyMetadata(false, OnChanged));

        public string RestorePath { get => (string)GetValue(RestorePathProperty); set => SetValue(RestorePathProperty, value); }
        public string FileSize    { get => (string)GetValue(FileSizeProperty);    set => SetValue(FileSizeProperty, value); }
        public string FileType    { get => (string)GetValue(FileTypeProperty);    set => SetValue(FileTypeProperty, value); }
        public bool   IsFolder    { get => (bool)GetValue(IsFolderProperty);      set => SetValue(IsFolderProperty, value); }

        public ArchiveFileRow()
        {
            InitializeComponent();
        }

        private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ArchiveFileRow r) r.Refresh();
        }

        private void Refresh()
        {
            TbName.Text = RestorePath;
            TbSize.Text = FileSize;
            TbType.Text = FileType;

            if (IsFolder)
            {
                IconPath.Data = Geometry.Parse("M20 6h-8l-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V8c0-1.1-.9-2-2-2z");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            else
            {
                // Document / file icon
                IconPath.Data = Geometry.Parse("M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.89 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z");
                IconPath.Fill = new SolidColorBrush(Color.FromRgb(0x89, 0x92, 0xAD));
            }
        }
    }
}
