using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using SimpleSshClient.Models;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class ConnectionDialog : Window
    {
        private readonly ConnectionStorageService _storageService;
        private List<ConnectionInfo> _connections;
        private readonly ConnectionInfo? _editingConnection;
        public ConnectionInfo? SelectedConnection { get; private set; }

        public ConnectionDialog(ConnectionStorageService storageService) : this(storageService, null) { }

        public ConnectionDialog(ConnectionStorageService storageService, ConnectionInfo? editingConnection)
        {
            _storageService = storageService;
            _connections = _storageService.LoadConnections();
            _editingConnection = editingConnection;
            InitializeComponent();

            if (_editingConnection != null)
            {
                Title = "编辑连接";
                NameTextBox.Text = _editingConnection.Name;
                HostTextBox.Text = _editingConnection.Host;
                PortTextBox.Text = _editingConnection.Port.ToString();
                UsernameTextBox.Text = _editingConnection.Username;
                PasswordBox.Password = _editingConnection.Password;
                PrivateKeyPathTextBox.Text = _editingConnection.PrivateKeyPath ?? string.Empty;

                if (!string.IsNullOrEmpty(_editingConnection.PrivateKeyPath))
                {
                    PrivateKeyRadio.IsChecked = true;
                }
                else
                {
                    PasswordRadio.IsChecked = true;
                }
            }
        }

        private void AuthMethod_Checked(object sender, RoutedEventArgs e)
        {
            UpdateAuthMethodUI();
        }

        private void UpdateAuthMethodUI()
        {
            if (PasswordBox == null || PrivateKeyPathTextBox == null || BrowsePrivateKeyButton == null || PasswordRadio == null || PrivateKeyRadio == null)
                return;

            PasswordBox.IsEnabled = PasswordRadio.IsChecked == true;
            PrivateKeyPathTextBox.IsEnabled = PrivateKeyRadio.IsChecked == true;
            BrowsePrivateKeyButton.IsEnabled = PrivateKeyRadio.IsChecked == true;
        }

        private void BrowsePrivateKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "选择私钥文件",
                Filter = "所有文件 (*.*)|*.*|PEM文件 (*.pem)|*.pem|密钥文件 (*.key)|*.key"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                PrivateKeyPathTextBox.Text = openFileDialog.FileName;
            }
        }

        private void SaveAndConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("请输入连接名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(HostTextBox.Text))
            {
                MessageBox.Show("请输入主机地址", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (PasswordRadio.IsChecked == true && string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                MessageBox.Show("请输入密码", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (PrivateKeyRadio.IsChecked == true && string.IsNullOrWhiteSpace(PrivateKeyPathTextBox.Text))
            {
                MessageBox.Show("请选择私钥文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var connection = CreateConnectionFromInput();

            if (_editingConnection != null)
            {
                // 基于ID查找并移除旧连接，确保即使对象引用不同也能正确移除
                var existingConnection = _connections.FirstOrDefault(c => c.Id == _editingConnection.Id);
                if (existingConnection != null)
                {
                    _connections.Remove(existingConnection);
                }
                _connections.Add(connection);
            }
            else
            {
                var existing = _connections.FirstOrDefault(c => c.Name == connection.Name);
                if (existing != null)
                {
                    var result = MessageBox.Show("已存在同名连接，是否覆盖?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        _connections.Remove(existing);
                        _connections.Add(connection);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    _connections.Add(connection);
                }
            }

            _storageService.SaveConnections(_connections);

            SelectedConnection = connection;
            DialogResult = true;
            Close();
        }

        private ConnectionInfo CreateConnectionFromInput()
        {
            return new ConnectionInfo
            {
                Id = _editingConnection?.Id ?? Guid.NewGuid().ToString(),
                Name = NameTextBox.Text.Trim(),
                Host = HostTextBox.Text.Trim(),
                Port = int.TryParse(PortTextBox.Text, out var port) ? port : 22,
                Username = UsernameTextBox.Text.Trim(),
                Password = PasswordBox.Password,
                PrivateKeyPath = PrivateKeyRadio.IsChecked == true ? PrivateKeyPathTextBox.Text.Trim() : null
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
