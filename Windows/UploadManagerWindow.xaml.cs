using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SimpleSshClient.Models;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class UploadManagerWindow : Window
    {
        public UploadManagerWindow()
        {
            InitializeComponent();
            UploadListView.ItemsSource = UploadManager.Instance.UploadTasks;
            UpdateStatusText();

            // 订阅任务事件
            UploadManager.Instance.TaskAdded += OnTaskAdded;
            UploadManager.Instance.TaskCompleted += OnTaskCompleted;
        }

        private void OnTaskAdded(object? sender, UploadTask e)
        {
            UpdateStatusText();
        }

        private void OnTaskCompleted(object? sender, UploadTask e)
        {
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            int totalTasks = UploadManager.Instance.UploadTasks.Count;
            int completedTasks = UploadManager.Instance.UploadTasks.Count(t => t.Status == UploadStatus.Completed);
            int failedTasks = UploadManager.Instance.UploadTasks.Count(t => t.Status == UploadStatus.Failed);
            int uploadingTasks = UploadManager.Instance.UploadTasks.Count(t => t.Status == UploadStatus.Uploading);

            StatusText.Text = $"总任务: {totalTasks}, 上传中: {uploadingTasks}, 已完成: {completedTasks}, 失败: {failedTasks}";
        }

        private void BtnClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            UploadManager.Instance.ClearCompletedTasks();
            UpdateStatusText();
        }

        private void BtnOpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is UploadTask task)
            {
                UploadManager.Instance.OpenFile(task);
            }
        }

        private void BtnOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is UploadTask task)
            {
                UploadManager.Instance.OpenFileLocation(task);
            }
        }

        private void BtnRemoveTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is UploadTask task)
            {
                UploadManager.Instance.RemoveTask(task);
                UpdateStatusText();
            }
        }
    }

    public class UploadStatusToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UploadStatus status && parameter is string param)
            {
                return param switch
                {
                    "Completed" => status == UploadStatus.Completed,
                    _ => true
                };
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
