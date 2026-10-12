using System;
using System.Windows;

namespace tpkursa4
{
    public partial class LoginWindow : Window
    {
        public UserAccount AuthenticatedUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Введите логин и пароль.", "Предупреждение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var user = AuthService.CreateDefault().TryLogin(username, password);
                if (user != null)
                {
                    AuthenticatedUser = user;
                    DialogResult = true;
                }
                else
                {
                    MessageBox.Show("Неверный логин или пароль.", "Ошибка доступа",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обращении к таблице пользователей:\n{ex.Message}",
                    "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
