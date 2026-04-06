using System;
using System.IO;
using System.Windows;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class SettingsWindow : Window
    {
        private readonly ThemeService _themeService;

        public SettingsWindow(ThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService;
            
            // 设置最后更新时间为应用构建时间
            SetLastUpdateTime();
        }

        public SettingsWindow() : this(new ThemeService())
        {
        }

        private void SetLastUpdateTime()
        {
            try
            {
                // 获取当前程序集的位置
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var assemblyPath = assembly.Location;
                
                // 获取文件的最后修改时间（作为构建时间）
                var lastWriteTime = File.GetLastWriteTime(assemblyPath);
                
                // 显示最后更新时间
                TbLastUpdate.Text = $"最后更新时间: {lastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")}";
            }
            catch
            {
                TbLastUpdate.Text = "最后更新时间: 未知";
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnTheme_Click(object sender, RoutedEventArgs e)
        {
            var themeWindow = new ThemeSelectorWindow(_themeService);
            themeWindow.Owner = this;
            themeWindow.ShowDialog();
        }

        private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开日志文件所在目录
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                var logDirectory = Path.Combine(exeDir, "logs");
                
                if (Directory.Exists(logDirectory))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logDirectory);
                }
                else
                {
                    MessageBox.Show("日志目录不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开日志目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearLogs_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 清空日志文件
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                var logDirectory = Path.Combine(exeDir, "logs");
                
                if (Directory.Exists(logDirectory))
                {
                    var logFiles = Directory.GetFiles(logDirectory);
                    foreach (var logFile in logFiles)
                    {
                        try
                        {
                            File.Delete(logFile);
                        }
                        catch { }
                    }
                    MessageBox.Show("日志已清空", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("日志目录不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清空日志失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void BtnSoftwareHome_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 使用ProcessStartInfo并设置UseShellExecute = true，确保在Windows上正确打开URL
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://shenmeji.com/ssh/",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开软件主页失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnConnectionPoolSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new ConnectionPoolSettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }
    }
}
