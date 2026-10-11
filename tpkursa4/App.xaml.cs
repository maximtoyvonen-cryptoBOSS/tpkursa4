using System.Windows;

namespace tpkursa4
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 1. ВРЕМЕННО переводим приложение в ручной режим управления жизнью.
            // Теперь закрытие окна логина НЕ вызовет автоматическое уничтожение программы.
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            LoginWindow loginWindow = new LoginWindow();

            if (loginWindow.ShowDialog() == true)
            {
                MainWindow mainWindow = new MainWindow();

                // 2. ВОЗВРАЩАЕМ стандартный режим, но привязываем его к ГЛАВНОМУ окну.
                // Теперь приложение закроется только тогда, когда юзер закроет MainWindow.
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;

                mainWindow.Show();
            }
            else
            {
                // Если авторизация не прошла (нажали "Отмена" или крестик на логине) — 
                // гасим приложение принудительно, так как режим ручной.
                Shutdown();
            }
        }
    }
}