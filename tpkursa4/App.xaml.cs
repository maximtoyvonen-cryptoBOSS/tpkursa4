using System.Windows;

namespace tpkursa4
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = new LoginWindow();
            if (loginWindow.ShowDialog() == true)
            {
                var mainWindow = new MainWindow(loginWindow.AuthenticatedUser);
                Current.ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
