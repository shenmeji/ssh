using System;
using System.Windows;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class ConnectionPoolSettingsWindow : Window
    {
        public ConnectionPoolSettingsWindow()
        {
            InitializeComponent();
            // 加载当前设置
            TxtMaxConnections.Text = ConnectionPool.Instance.MaxConnections.ToString();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(TxtMaxConnections.Text, out int maxConnections))
            {
                if (maxConnections >= 1 && maxConnections <= 100)
                {
                    ConnectionPool.Instance.MaxConnections = maxConnections;
                    MessageBox.Show("连接池设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show("最大连接数必须在1-100之间", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("请输入有效的数字", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}