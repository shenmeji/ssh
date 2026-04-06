using System;
using System.Collections.Generic;
using System.Windows;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class ConnectionStatsWindow : Window
    {
        public ConnectionStatsWindow()
        {
            InitializeComponent();
            RefreshStats();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshStats();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshStats()
        {
            var stats = ConnectionPool.Instance.GetConnectionStats();
            
            // 更新总连接数和总通道数
            TotalConnectionsText.Text = stats.Count.ToString();
            
            int totalChannels = 0;
            foreach (var stat in stats)
            {
                totalChannels += stat.UsageCount;
            }
            TotalChannelsText.Text = totalChannels.ToString();
            
            // 更新连接列表
            ConnectionsListView.ItemsSource = stats;
        }
    }

    public class ConnectionStat
    {
        public string? Key { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Username { get; set; }
        public int UsageCount { get; set; }
        public DateTime? ConnectTime { get; set; }
        public DateTime? LastUsed { get; set; }
        public string? Status { get; set; }
    }
}