using System.Windows;

namespace tpkursa4
{
    public partial class MainWindow : Window
    {
        public MainWindow() : this(null)
        {
        }

        public MainWindow(UserAccount user)
        {
            InitializeComponent();
            DataContext = new MainViewModel(DbRepository.CreateDefault(), user);
        }
    }
}
