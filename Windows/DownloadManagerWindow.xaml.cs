using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SimpleSshClient.Models;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class DownloadManagerWindow : Window
    {
        public DownloadManagerWindow()
        {
            InitializeComponent();
            DownloadListView.ItemsSource = DownloadManager.Instance.DownloadTasks;
            UpdateStatus();

            // 订阅任务变化事件
            DownloadManager.Instance.TaskAdded += (s, e) => UpdateStatus();
            DownloadManager.Instance.TaskCompleted += (s, e) => UpdateStatus();
        }

        private void UpdateStatus()
        {
            var manager = DownloadManager.Instance;
            int total = manager.DownloadTasks.Count;
            int downloading = 0;
            int completed = 0;

            foreach (var task in manager.DownloadTasks)
            {
                if (task.Status == DownloadStatus.Downloading)
                    downloading++;
                else if (task.Status == DownloadStatus.Completed)
                    completed++;
            }

            StatusText.Text = $"总任务: {total} | 下载中: {downloading} | 已完成: {completed}";
        }

        private void BtnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            DownloadManager.Instance.ClearCompletedTasks();
            UpdateStatus();
        }

        private void BtnOpenDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            // 获取默认下载文件夹
            string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (Directory.Exists(downloadPath))
            {
                Process.Start("explorer.exe", downloadPath);
            }
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DownloadTask task)
            {
                DownloadManager.Instance.OpenFile(task);
            }
        }

        private void BtnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DownloadTask task)
            {
                DownloadManager.Instance.OpenFileLocation(task);
            }
        }

        private void BtnRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DownloadTask task)
            {
                DownloadManager.Instance.RemoveTask(task);
                UpdateStatus();
            }
        }
    }

    // 状态转换器
    public class DownloadStatusToEnabledConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is DownloadStatus status && parameter is string requiredStatus)
            {
                return status.ToString() == requiredStatus;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
