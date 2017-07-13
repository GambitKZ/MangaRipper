using NLog;
using System.Windows;

namespace MangaRipperWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public MainWindow()
        {
            Logger.Info("> Main()");
            InitializeComponent();
            Logger.Info("< Main()");
        }
    }
}
