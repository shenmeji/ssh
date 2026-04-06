using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SimpleSshClient.Models;
using SimpleSshClient.Services;
using SimpleSshClient.Windows;

namespace SimpleSshClient
{
    public class TerminalTabData
    {
        public ConnectionInfo Connection { get; set; }
        public SshService SshService { get; set; }
        public TerminalControl TerminalControl { get; set; }
        public DateTime ConnectionStartTime { get; set; } = DateTime.Now;
        public bool IsClosing { get; set; } = false;

        public TerminalTabData(ConnectionInfo connection, SshService sshService, TerminalControl terminalControl)
        {
            Connection = connection;
            SshService = sshService;
            TerminalControl = terminalControl;
        }
    }

    public partial class MainWindow : Window
    {
        private readonly ConnectionStorageService _connectionStorageService;
        private readonly ThemeService _themeService;
        private readonly LogService _logService = new LogService();
        private readonly List<TerminalTabData> _terminalTabs = new();
        private System.Windows.Threading.DispatcherTimer _statusTimer;

        public MainWindow()
        {
            InitializeComponent();
            _connectionStorageService = new ConnectionStorageService();
            _themeService = new ThemeService();
            // 订阅主题更改事件
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
            TabControl.SelectionChanged += TabControl_SelectionChanged;
            
            // 初始化状态栏更新定时器
            _statusTimer = new System.Windows.Threading.DispatcherTimer();
            _statusTimer.Interval = TimeSpan.FromSeconds(1);
            _statusTimer.Tick += StatusTimer_Tick;
        }

        private void ThemeService_ThemeChanged(object sender, Models.TerminalTheme theme)
        {
            // 遍历所有已打开的终端连接，应用新主题
            foreach (var tabData in _terminalTabs)
            {
                tabData.TerminalControl.ApplyTheme(theme);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _logService.Info("主窗口加载");
            LoadWindowState();
            _statusTimer.Start();
            _logService.Info("主窗口加载完成");
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _logService.Info("主窗口关闭");
            _statusTimer.Stop();
            SaveWindowState();
            // 关闭SFTP文件浏览器窗口
            CloseSftpWindow();
            // 关闭所有连接
            CloseAllConnections();
            _logService.Info("主窗口已关闭");
            // 确保应用完全退出
            System.Windows.Application.Current.Shutdown();
        }

        private void LoadWindowState()
        {
            var windowState = WindowStateService.Load();
            if (windowState != null)
            {
                Left = windowState.Left;
                Top = windowState.Top;
                Width = Math.Max(800, windowState.Width);
                Height = Math.Max(600, windowState.Height);
                if (windowState.IsMaximized)
                {
                    WindowState = System.Windows.WindowState.Maximized;
                }
            }
        }

        private void SaveWindowState()
        {
            var windowState = new WindowPosition
            {
                Left = Left,
                Top = Top,
                Width = Width,
                Height = Height,
                IsMaximized = WindowState == System.Windows.WindowState.Maximized
            };
            WindowStateService.Save(windowState);
        }

        private void ShowConnectionManager()
        {
            var managerWindow = new ConnectionManagerWindow(_connectionStorageService);
            managerWindow.Owner = this;
            if (managerWindow.ShowDialog() == true && managerWindow.SelectedConnection != null)
            {
                ConnectToServer(managerWindow.SelectedConnection);
            }
        }

        private void ConnectToServer(ConnectionInfo connection)
        {
            try
            {
                _logService.Info($"开始连接到服务器: {connection.Host}:{connection.Port}");
                StatusText.Text = "连接中...";
                
                // 在UI线程中初始化连接和UI元素
                var (sshService, terminalControl, tabData, tabItem) = InitializeConnection(connection);
                SetupConnectionEvents(sshService, terminalControl);
                AddConnectionToUI(tabItem, connection);
                
                // 异步连接，避免阻塞UI
                Task.Run(async () =>
                {
                    try
                    {
                        await ConnectAsync(sshService, connection, tabData, tabItem);
                    }
                    catch (Exception ex)
                    {
                        // 在UI线程中显示错误信息
                        Dispatcher.Invoke(() =>
                        {
                            _logService.Error("连接过程中发生错误", ex);
                            StatusText.Text = "连接失败";
                            string errorMessage = GetFriendlyErrorMessage(ex);
                            MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            CloseTab(tabItem);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                _logService.Error("创建连接过程中发生错误", ex);
                StatusText.Text = "连接失败";
                string errorMessage = GetFriendlyErrorMessage(ex);
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private (SshService, TerminalControl, TerminalTabData, TabItem) InitializeConnection(ConnectionInfo connection)
        {
            var sshService = new SshService();
            var terminalControl = new TerminalControl();
            var tabData = new TerminalTabData(connection, sshService, terminalControl);
            
            terminalControl.SetSshService(sshService);
            terminalControl.ApplyTheme(_themeService.GetCurrentTheme());
            
            // 添加到连接管理器
            ConnectionManagerService.Instance.AddConnection(connection, sshService);
            
            // 先创建tabItem，不设置Header
            var tabItem = new TabItem
            {
                Content = terminalControl,
                Tag = tabData
            };
            
            // 创建TabHeader并设置Tag
            var header = CreateTabHeader(connection.Name, tabItem);
            tabItem.Header = header;
            
            _terminalTabs.Add(tabData);
            
            return (sshService, terminalControl, tabData, tabItem);
        }

        private void SetupConnectionEvents(SshService sshService, TerminalControl terminalControl)
        {
            // 连接事件处理
            sshService.OutputReceived += (s, output) => terminalControl.AppendOutput(output);
            terminalControl.CommandSent += (s, cmd) => sshService.SendCommand(cmd);
            terminalControl.InterruptRequested += (s, e) => sshService.SendInterrupt();
        }

        private void AddConnectionToUI(TabItem tabItem, ConnectionInfo connection)
        {
            // 如果是第一个连接，移除欢迎标签
            if (_terminalTabs.Count == 1 && TabControl.Items.Count > 0 && TabControl.Items[0] is TabItem welcomeTab && welcomeTab.Header.ToString() == "欢迎")
            {
                TabControl.Items.Remove(welcomeTab);
            }
            
            TabControl.Items.Add(tabItem);
            TabControl.SelectedItem = tabItem;
        }

        private async Task ConnectAsync(SshService sshService, ConnectionInfo connection, TerminalTabData tabData, TabItem tabItem)
        {
            try
            {
                await sshService.ConnectAsync(connection);
                
                // 检查标签页是否正在关闭
                if (tabData.IsClosing)
                    return;
                
                // 获取主机名
                var hostname = await sshService.GetHostnameAsync();
                
                Dispatcher.Invoke(() =>
                {
                    // 再次检查标签页是否正在关闭
                    if (tabData.IsClosing)
                        return;
                    
                    UpdateStatusBar(connection, hostname, true);
                    _logService.Info($"成功连接到服务器: {connection.Host}:{connection.Port}");
                });
            }
            catch (Exception ex)
            {
                _logService.Error($"连接服务器失败: {connection.Host}:{connection.Port}", ex);
                
                Dispatcher.Invoke(() =>
                {
                    // 如果标签页正在关闭，不显示错误弹窗
                    if (tabData.IsClosing)
                        return;
                    
                    StatusText.Text = "连接失败";
                    // 提供友好的错误提示，避免直接暴露异常详情
                    string errorMessage = GetFriendlyErrorMessage(ex);
                    MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    CloseTab(tabItem);
                });
            }
        }

        private string GetFriendlyErrorMessage(Exception ex)
        {
            if (ex.InnerException != null)
            {
                if (ex.InnerException is Renci.SshNet.Common.SshConnectionException)
                {
                    return "SSH连接失败，请检查网络连接和服务器设置";
                }
                else if (ex.InnerException is Renci.SshNet.Common.SshAuthenticationException)
                {
                    return "认证失败，请检查用户名和密码";
                }
            }
            return ex.Message;
        }

        private UIElement CreateTabHeader(string connectionName, TabItem tabItem)
        {
            var grid = new Grid { Width = 200, HorizontalAlignment = HorizontalAlignment.Stretch };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var textBlock = new TextBlock
            {
                Text = connectionName,
                Margin = new Thickness(5, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(textBlock, 0);
            
            var closeButton = new Button
            {
                Content = "×",
                Width = 20,
                Height = 20,
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = tabItem // 设置Tag为当前TabItem
            };
            closeButton.Click += (s, e) =>
            {
                var btn = (Button)s;
                var tabItemFromTag = btn.Tag as TabItem;
                if (tabItemFromTag != null)
                {
                    CloseTab(tabItemFromTag);
                }
            };
            Grid.SetColumn(closeButton, 1);
            
            grid.Children.Add(textBlock);
            grid.Children.Add(closeButton);
            
            return grid;
        }

        private void UpdateStatusBar(ConnectionInfo connection, string hostname, bool connected)
        {
            var status = connected ? "已连接" : "未连接";
            StatusText.Text = $"{status} | {connection.Username}@{connection.Host}:{connection.Port}";
            
            // 更新连接状态指示器
            if (connected)
            {
                ConnectionStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                if (hostname != "")
                {
                    ConnectionInfoText.Text = hostname;
                }
                else
                {
                    ConnectionInfoText.Text = "";
                }
            }
            else
            {
                ConnectionStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
                ConnectionInfoText.Text = "未连接";
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            var currentTab = GetCurrentTabData();
            if (currentTab != null)
            {
                var connectionTime = currentTab.ConnectionStartTime;
                var duration = DateTime.Now - connectionTime;
                
                ConnectionTimeText.Text = $"自{connectionTime.ToString("yyyy-MM-dd HH:mm:ss")} 持续: {FormatDuration(duration)}";
            }
            else
            {
                ConnectionTimeText.Text = "";
            }
        }

        private string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalHours >= 1)
            {
                return $"{duration.Hours}时{duration.Minutes}分{duration.Seconds}秒";
            }
            else if (duration.TotalMinutes >= 1)
            {
                return $"{duration.Minutes}分{duration.Seconds}秒";
            }
            else
            {
                return $"{duration.Seconds}秒";
            }
        }

        private void CloseTab(TabItem tabItem)
        {
            if (tabItem.Tag is TerminalTabData tabData)
            {
                var connectionName = tabData.Connection.Name;
                _logService.Info($"关闭连接: {connectionName}");
                
                // 设置关闭标志，避免连接过程中显示错误弹窗
                tabData.IsClosing = true;
                
                tabData.SshService.Disconnect();
                // 从连接管理器中移除连接
                ConnectionManagerService.Instance.RemoveConnection(tabData.SshService);
                _terminalTabs.Remove(tabData);
                _logService.Info($"连接已关闭: {connectionName}");
            }
            TabControl.Items.Remove(tabItem);
            
            if (TabControl.Items.Count == 0)
            {
                AddWelcomeTab();
                ResetStatusBar();
            }
        }

        private void AddWelcomeTab()
        {
            // 清空TabControl并重新添加欢迎标签
            TabControl.Items.Clear();
            
            // 添加与XAML中相同的欢迎标签
            var welcomeTab = new TabItem
            {
                Header = "欢迎",
                IsEnabled = false
            };
            
            var grid = new Grid();
            grid.Background = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            
            var stackPanel = new StackPanel();
            stackPanel.VerticalAlignment = VerticalAlignment.Center;
            stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
            stackPanel.Margin = new Thickness(20);
            stackPanel.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            
            var titleText = new TextBlock();
            titleText.Text = "什么机";
            titleText.FontSize = 24;
            titleText.FontWeight = FontWeights.Bold;
            titleText.Margin = new Thickness(0, 0, 0, 20);
            
            var statusText = new TextBlock();
            statusText.Text = "当前无活动连接";
            statusText.FontSize = 16;
            statusText.Margin = new Thickness(0, 0, 0, 10);
            
            var instructionText = new TextBlock();
            instructionText.Text = "请使用以下功能开始：";
            instructionText.FontSize = 14;
            instructionText.Margin = new Thickness(0, 0, 0, 20);
            
            var buttonPanel = new StackPanel();
            buttonPanel.Orientation = Orientation.Horizontal;
            buttonPanel.HorizontalAlignment = HorizontalAlignment.Center;
            buttonPanel.Margin = new Thickness(0, 0, 0, 10);
            
            var manageConnectionsButton = new Button();
            manageConnectionsButton.Content = "管理连接";
            manageConnectionsButton.Margin = new Thickness(5, 5, 5, 5);
            manageConnectionsButton.Padding = new Thickness(10, 5, 10, 5);
            manageConnectionsButton.Click += BtnManageConnections_Click;
            
            var sftpBrowserButton = new Button();
            sftpBrowserButton.Content = "SFTP浏览器";
            sftpBrowserButton.Margin = new Thickness(5, 5, 5, 5);
            sftpBrowserButton.Padding = new Thickness(10, 5, 10, 5);
            sftpBrowserButton.Click += BtnSftpBrowser_Click;
            
            buttonPanel.Children.Add(manageConnectionsButton);
            buttonPanel.Children.Add(sftpBrowserButton);
            
            var footerText = new TextBlock();
            footerText.Text = "或使用顶部工具栏的按钮访问其他功能";
            footerText.FontSize = 12;
            footerText.Margin = new Thickness(0, 20, 0, 0);
            footerText.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
            
            stackPanel.Children.Add(titleText);
            stackPanel.Children.Add(statusText);
            stackPanel.Children.Add(instructionText);
            stackPanel.Children.Add(buttonPanel);
            stackPanel.Children.Add(footerText);
            
            grid.Children.Add(stackPanel);
            welcomeTab.Content = grid;
            
            TabControl.Items.Add(welcomeTab);
            TabControl.SelectedItem = welcomeTab;
        }

        private void ResetStatusBar()
        {
            StatusText.Text = "无连接";
            ConnectionStatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            ConnectionInfoText.Text = "";
            ConnectionTimeText.Text = "";
        }

        private void CloseAllConnections()
        {
            _logService.Info("开始关闭所有连接");
            foreach (var tabData in _terminalTabs)
            {
                var connectionName = tabData.Connection.Name;
                _logService.Info($"关闭连接: {connectionName}");
                try
                {
                    tabData.SshService.Disconnect();
                    // 从连接管理器中移除连接
                    ConnectionManagerService.Instance.RemoveConnection(tabData.SshService);
                    // 调用Dispose方法确保所有资源都被释放
                    tabData.SshService.Dispose();
                }
                catch (Exception ex)
                {
                    _logService.Error($"关闭连接时发生错误: {connectionName}", ex);
                }
                _logService.Info($"连接已关闭: {connectionName}");
            }
            _terminalTabs.Clear();
            
            AddWelcomeTab();
            ResetStatusBar();
            
            _logService.Info("所有连接已关闭");
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private TerminalTabData? GetCurrentTabData()
        {
            if (TabControl.SelectedItem is TabItem item && item.Tag is TerminalTabData tabData)
            {
                return tabData;
            }
            return null;
        }

        private void BtnNewConnection_Click(object sender, RoutedEventArgs e)
        {
            ShowConnectionManager();
        }

        private void BtnQuickCommand_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QuickCommandDialog();
            if (this.IsLoaded && this.IsVisible)
            {
                dialog.Owner = this;
            }
            
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Command))
            {
                var currentTab = GetCurrentTabData();
                if (currentTab != null)
                {
                    // 直接使用 SshService 发送命令
                    currentTab.SshService.SendCommand(dialog.Command);
                }
                else
                {
                    MessageBox.Show("必须先连接到服务器才能执行命令", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private SftpFileBrowserWindow? _sftpWindow;

        private void BtnSftpBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_sftpWindow == null)
            {
                _sftpWindow = new SftpFileBrowserWindow();
                _sftpWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _sftpWindow.Owner = this;
                _sftpWindow.Topmost = false; // 确保窗口不是总是在最前面
                _sftpWindow.Closed += (s, args) => 
                {
                    _sftpWindow = null;
                    // 当SFTP窗口关闭时，激活主窗口，确保应用保持在前台
                    this.Activate();
                };
                _sftpWindow.Show();
            }
            else
            {
                // 切换到已打开的窗口，即使它被最小化
                _sftpWindow.WindowState = WindowState.Normal;
                _sftpWindow.Topmost = false; // 确保窗口不是总是在最前面
            }
        }

        private void CloseSftpWindow()
        {
            if (_sftpWindow != null && _sftpWindow.IsVisible)
            {
                _sftpWindow.Close();
                _sftpWindow = null;
            }
        }



        private void BtnManageConnections_Click(object sender, RoutedEventArgs e)
        {
            ShowConnectionManager();
        }

        private SettingsWindow? _settingsWindow;

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new Windows.SettingsWindow(_themeService);
                _settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _settingsWindow.Owner = this;
                _settingsWindow.Closed += (s, args) => 
                {
                    _settingsWindow = null;
                    // 当设置窗口关闭时，激活主窗口，确保应用保持在前台
                    this.Activate();
                };
                _settingsWindow.Show();
            }
            else
            {
                // 切换到已打开的窗口，即使它被最小化
                _settingsWindow.WindowState = WindowState.Normal;
                _settingsWindow.Activate();
            }
        }

        private Windows.ConnectionStatsWindow? _connectionStatsWindow;

        private void BtnConnectionStats_Click(object sender, RoutedEventArgs e)
        {
            if (_connectionStatsWindow == null)
            {
                _connectionStatsWindow = new Windows.ConnectionStatsWindow();
                _connectionStatsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _connectionStatsWindow.Owner = this;
                _connectionStatsWindow.Topmost = false; // 确保窗口不是总是在最前面
                _connectionStatsWindow.Closed += (s, args) => 
                {
                    _connectionStatsWindow = null;
                    // 当连接状态窗口关闭时，激活主窗口，确保应用保持在前台
                    this.Activate();
                };
                _connectionStatsWindow.Show();
            }
            else
            {
                // 切换到已打开的窗口，即使它被最小化
                _connectionStatsWindow.WindowState = WindowState.Normal;
                _connectionStatsWindow.Topmost = false; // 确保窗口不是总是在最前面
            }
        }
    }
}
