using System;
using System.Windows;

namespace tpkursa4
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Пожалуйста, введите логин!", "Предупреждение",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Бизнес-логика в AuthService, здесь только View.
                if (AuthService.CreateDefault().UserExists(username))
                {
                    this.DialogResult = true;
                }
                else
                {
                    MessageBox.Show($"Пользователь '{username}' не найден в системе!", "Ошибка доступа",
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
            this.DialogResult = false;
        }
    }
}
