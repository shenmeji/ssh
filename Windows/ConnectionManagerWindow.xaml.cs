using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SimpleSshClient.Models;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class ConnectionManagerWindow : Window
    {
        private readonly ConnectionStorageService _storageService;
        private List<ConnectionInfo> _connections;
        public ConnectionInfo? SelectedConnection { get; private set; }

        public ConnectionManagerWindow(ConnectionStorageService storageService)
        {
            InitializeComponent();
            _storageService = storageService;
            _connections = _storageService.LoadConnections();
            ConnectionsListBox.ItemsSource = _connections;
        }

        private void ConnectionsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            EditButton.IsEnabled = ConnectionsListBox.SelectedItem != null;
            DeleteButton.IsEnabled = ConnectionsListBox.SelectedItem != null;
            ConnectButton.IsEnabled = ConnectionsListBox.SelectedItem != null;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConnectionDialog(_storageService) { Owner = this };
            dialog.ShowDialog();
            RefreshConnections();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionsListBox.SelectedItem is not ConnectionInfo connection)
                return;

            var dialog = new ConnectionDialog(_storageService, connection) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                RefreshConnections();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionsListBox.SelectedItem is not ConnectionInfo connection)
                return;

            var result = MessageBox.Show($"确定要删除连接 \"{connection.Name}\" 吗?", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _connections.Remove(connection);
                _storageService.SaveConnections(_connections);
                RefreshConnections();
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_connections.Count == 0)
            {
                MessageBox.Show("没有可导出的连接", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // 先显示导出方式选择对话框
                var exportDialog = new Window
                {
                    Title = "选择导出方式",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.SingleBorderWindow,
                    Topmost = true,
                    ShowInTaskbar = false,
                    SizeToContent = SizeToContent.WidthAndHeight
                };

                var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

                var plainTextRadio = new System.Windows.Controls.RadioButton
                {
                    Content = "明文导出（不推荐）",
                    IsChecked = false,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                System.Windows.Controls.Grid.SetRow(plainTextRadio, 0);
                System.Windows.Controls.Grid.SetColumnSpan(plainTextRadio, 2);

                var encryptedRadio = new System.Windows.Controls.RadioButton
                {
                    Content = "设置密码（至少6位）",
                    IsChecked = true,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                System.Windows.Controls.Grid.SetRow(encryptedRadio, 1);
                System.Windows.Controls.Grid.SetColumnSpan(encryptedRadio, 2);

                var passwordLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "密码：",
                    Margin = new Thickness(0, 0, 10, 5),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                System.Windows.Controls.Grid.SetRow(passwordLabel, 2);
                System.Windows.Controls.Grid.SetColumn(passwordLabel, 0);

                var passwordBox = new System.Windows.Controls.PasswordBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                System.Windows.Controls.Grid.SetRow(passwordBox, 2);
                System.Windows.Controls.Grid.SetColumn(passwordBox, 1);

                var confirmPasswordLabel = new System.Windows.Controls.TextBlock
                {
                    Text = "确认密码：",
                    Margin = new Thickness(0, 0, 10, 5),
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                System.Windows.Controls.Grid.SetRow(confirmPasswordLabel, 3);
                System.Windows.Controls.Grid.SetColumn(confirmPasswordLabel, 0);

                var confirmPasswordBox = new System.Windows.Controls.PasswordBox
                {
                    Width = 200,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                System.Windows.Controls.Grid.SetRow(confirmPasswordBox, 3);
                System.Windows.Controls.Grid.SetColumn(confirmPasswordBox, 1);

                // 密码框启用/禁用逻辑
                encryptedRadio.Checked += (s, e) => 
                {
                    passwordBox.IsEnabled = true;
                    confirmPasswordBox.IsEnabled = true;
                    passwordLabel.IsEnabled = true;
                    confirmPasswordLabel.IsEnabled = true;
                };
                plainTextRadio.Checked += (s, e) => 
                {
                    passwordBox.IsEnabled = false;
                    confirmPasswordBox.IsEnabled = false;
                    passwordLabel.IsEnabled = false;
                    confirmPasswordLabel.IsEnabled = false;
                };

                // 默认启用密码框
                passwordBox.IsEnabled = true;
                confirmPasswordBox.IsEnabled = true;
                passwordLabel.IsEnabled = true;
                confirmPasswordLabel.IsEnabled = true;

                var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var okButton = new System.Windows.Controls.Button { Content = "确定", Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(15, 5, 15, 5) };
                var cancelButton = new System.Windows.Controls.Button { Content = "取消", Padding = new Thickness(15, 5, 15, 5) };
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                System.Windows.Controls.Grid.SetRow(buttonPanel, 4);
                System.Windows.Controls.Grid.SetColumnSpan(buttonPanel, 2);

                grid.Children.Add(plainTextRadio);
                grid.Children.Add(encryptedRadio);
                grid.Children.Add(passwordLabel);
                grid.Children.Add(passwordBox);
                grid.Children.Add(confirmPasswordLabel);
                grid.Children.Add(confirmPasswordBox);
                grid.Children.Add(buttonPanel);
                exportDialog.Content = grid;

                okButton.Click += (s, e) => { exportDialog.DialogResult = true; };
                cancelButton.Click += (s, e) => { exportDialog.DialogResult = false; };

                var dialogResult = exportDialog.ShowDialog();

                if (dialogResult == true)
                {
                    bool useEncryption = (bool)encryptedRadio.IsChecked!;
                    string password = passwordBox.Password;
                    string confirmPassword = confirmPasswordBox.Password;

                    if (useEncryption)
                    {
                        if (string.IsNullOrEmpty(password))
                        {
                            MessageBox.Show("请输入加密密码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        
                        if (password.Length < 6)
                        {
                            MessageBox.Show("密码长度至少6位", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                        
                        if (password != confirmPassword)
                        {
                            MessageBox.Show("两次输入的密码不一致", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }

                    // 然后选择保存位置
                    var saveDialog = new SaveFileDialog
                    {
                        Title = "导出连接",
                        Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                        FileName = $"shenmeji_ssh_{DateTime.Now.ToString("yyyyMMdd")}.json"
                    };

                    if (saveDialog.ShowDialog() == true)
                    {
                        _storageService.ExportConnections(saveDialog.FileName, _connections, useEncryption, password);
                        var keyConnections = _connections.Count(c => !string.IsNullOrEmpty(c.PrivateKeyPath));
                        var message = $"成功导出 {_connections.Count} 个连接";
                        if (useEncryption)
                        {
                            message += "\n\n提示：连接信息已使用密码加密。导入时需要输入相同的密码。";
                        }
                        if (keyConnections > 0)
                        {
                            message += $"\n\n提示：其中 {keyConnections} 个连接使用证书认证，证书文件路径已保存。\n请确保在目标设备上证书文件存在相同路径，或手动迁移证书文件。";
                        }
                        MessageBox.Show(message, "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "导入连接",
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    // 首先尝试无密码导入，检查文件是否有效以及加密类型
                    var importedConnections = _storageService.ImportConnections(openDialog.FileName);
                    
                    // 处理导入结果
                    ProcessImportedConnections(importedConnections);
                }
                catch (InvalidOperationException ex) when (ex.Message == "需要密码")
                {
                    // 需要密码，显示密码输入对话框
                    ShowPasswordDialog(openDialog.FileName);
                }
                catch (Exception ex)
                {
                    // 其他错误
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 显示密码输入对话框
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        private void ShowPasswordDialog(string filePath)
        {
            // 显示密码输入对话框
            var passwordDialog = new Window
            {
                Title = "输入密码",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                Topmost = true,
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            var titleText = new System.Windows.Controls.TextBlock
            {
                Text = "此文件使用密码加密，请输入密码",
                Margin = new Thickness(0, 0, 0, 10)
            };
            System.Windows.Controls.Grid.SetRow(titleText, 0);

            var passwordBox = new System.Windows.Controls.PasswordBox
            {
                Width = 200,
                Margin = new Thickness(0, 0, 0, 20)
            };
            System.Windows.Controls.Grid.SetRow(passwordBox, 1);

            var buttonPanel = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new System.Windows.Controls.Button { Content = "确定", Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(15, 5, 15, 5) };
            var cancelButton = new System.Windows.Controls.Button { Content = "取消", Padding = new Thickness(15, 5, 15, 5) };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 2);

            grid.Children.Add(titleText);
            grid.Children.Add(passwordBox);
            grid.Children.Add(buttonPanel);
            passwordDialog.Content = grid;

            bool? dialogResult = false;
            okButton.Click += (s, e) => { dialogResult = true; passwordDialog.Close(); };
            cancelButton.Click += (s, e) => { dialogResult = false; passwordDialog.Close(); };

            passwordDialog.ShowDialog();

            if (dialogResult == true)
            {
                string password = passwordBox.Password;
                if (!string.IsNullOrEmpty(password))
                {
                    try
                    {
                        // 使用密码导入
                        var importedConnections = _storageService.ImportConnections(filePath, password);
                        ProcessImportedConnections(importedConnections);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// 处理导入的连接
        /// </summary>
        /// <param name="importedConnections">导入的连接列表</param>
        private void ProcessImportedConnections(List<ConnectionInfo> importedConnections)
        {
            if (importedConnections.Count == 0)
            {
                MessageBox.Show("没有找到可导入的连接", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int importedCount = 0;
            int skippedCount = 0;

            // 使用HashSet存储现有连接的ID，提高查找效率
            var existingIds = new HashSet<string>(_connections.Select(c => c.Id));

            foreach (var conn in importedConnections)
            {
                if (!existingIds.Contains(conn.Id))
                {
                    _connections.Add(conn);
                    importedCount++;
                    existingIds.Add(conn.Id); // 添加到现有ID集合中，避免重复添加
                }
                else
                {
                    skippedCount++;
                }
            }

            if (importedCount > 0)
            {
                _storageService.SaveConnections(_connections);
                RefreshConnections();
            }

            string message = $"导入完成！\n\n成功导入: {importedCount} 个\n跳过(已存在): {skippedCount} 个\n总连接数: {_connections.Count} 个";
            
            if (importedCount > 0)
            {
                var keyConnections = importedConnections.Where(c => !string.IsNullOrEmpty(c.PrivateKeyPath)).ToList();
                var missingKeys = keyConnections.Count(c => !File.Exists(c.PrivateKeyPath));
                if (missingKeys > 0)
                {
                    message += $"\n\n警告：{missingKeys} 个证书认证连接的证书文件不存在，请检查证书路径。";
                }
            }
            
            MessageBox.Show(message, "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConnectionsListBox.SelectedItem is ConnectionInfo connection)
            {
                SelectedConnection = connection;
                DialogResult = true;
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ConnectionsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ConnectButton_Click(sender, e);
        }

        private void RefreshConnections()
        {
            _connections = _storageService.LoadConnections();
            ConnectionsListBox.ItemsSource = null;
            ConnectionsListBox.ItemsSource = _connections;
        }
    }
}
