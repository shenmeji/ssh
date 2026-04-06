using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SimpleSshClient.Models;

namespace SimpleSshClient.Windows
{
    public partial class MessageHistoryWindow : Window
    {
        public List<MessageItem> Messages { get; set; }

        public MessageHistoryWindow(List<MessageItem> messages)
        {
            InitializeComponent();
            // 创建messages列表的副本，避免修改原始列表
            Messages = new List<MessageItem>(messages);
            MessageListView.ItemsSource = Messages;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            Messages.Clear();
            MessageListView.Items.Refresh();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}