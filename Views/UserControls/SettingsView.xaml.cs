using System.Windows.Controls;
using DevEnvStudio.ViewModels;

namespace DevEnvStudio.Views.UserControls
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            DataContext = SettingsViewModel.Instance;
            SettingsViewModel.Instance.LoadFromSettings();
        }
    }
}