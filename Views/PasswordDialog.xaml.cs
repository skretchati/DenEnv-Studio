using System.Windows;
using System.Windows.Input;

namespace DevEnvStudio.Views
{
    /// <summary>
    /// Modal dialog for entering a backup password.
    /// Exposes the entered password via the Password property.
    /// </summary>
    public partial class PasswordDialog : Window
    {
        public string Password => PwdBox.Password;

        public PasswordDialog()
        {
            InitializeComponent();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void PwdBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
                Close();
            }
        }

        private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            PwdPlaceholder.Visibility = PwdBox.Password.Length == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}