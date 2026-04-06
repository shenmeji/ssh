using System.Windows;
using System.Windows.Media;
using SimpleSshClient.Models;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class ThemeSelectorWindow : Window
    {
        private readonly ThemeService _themeService;
        public TerminalTheme? SelectedTheme { get; private set; }

        public ThemeSelectorWindow(ThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService;
            LoadCurrentTheme();
        }

        private void LoadCurrentTheme()
        {
            SelectedTheme = _themeService.GetCurrentTheme();
            DataContext = SelectedTheme;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTheme != null)
            {
                var defaultTheme = _themeService.GetDefaultTheme();
                SelectedTheme.Background = defaultTheme.Background;
                SelectedTheme.Foreground = defaultTheme.Foreground;
                SelectedTheme.Cursor = defaultTheme.Cursor;
                SelectedTheme.Selection = defaultTheme.Selection;
                DataContext = null;
                DataContext = SelectedTheme;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedTheme != null)
            {
                _themeService.SaveTheme(SelectedTheme);
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}