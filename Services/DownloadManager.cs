using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SimpleSshClient.Models;

namespace SimpleSshClient.Services
{
    public class DownloadManager
    {
        private static DownloadManager? _instance;
        private static readonly object _lock = new();

        public static DownloadManager Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new DownloadManager();
                    return _instance;
                }
            }
        }

        public ObservableCollection<DownloadTask> DownloadTasks { get; } = new();
        public event EventHandler<DownloadTask>? TaskAdded;
        public event EventHandler<DownloadTask>? TaskCompleted;

        private DownloadManager()
        {
        }

        public DownloadTask AddDownloadTask(string fileName, string remotePath, string localPath, long totalBytes, SshService sshService)
        {
            // 获取连接标识
            string connectionId = string.Empty;
            if (sshService != null && sshService.IsConnected)
            {
                var connectionInfo = ConnectionManagerService.Instance.GetConnectionInfo(sshService);
                if (connectionInfo != null)
                {
                    connectionId = $"{connectionInfo.Host}:{connectionInfo.Port}:{connectionInfo.Username}";
                }
            }

            var task = new DownloadTask
            {
                FileName = fileName,
                RemotePath = remotePath,
                LocalPath = localPath,
                TotalBytes = totalBytes,
                Status = DownloadStatus.Pending,
                StartTime = DateTime.Now,
                ConnectionId = connectionId
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                DownloadTasks.Insert(0, task);
            });

            TaskAdded?.Invoke(this, task);

            // 启动下载
            _ = StartDownloadAsync(task, sshService);

            return task;
        }

        private async Task StartDownloadAsync(DownloadTask task, SshService sshService)
        {
            task.Status = DownloadStatus.Downloading;
            var cancellationTokenSource = new CancellationTokenSource();

            // 订阅进度事件
            EventHandler<DownloadProgressEventArgs>? progressHandler = null;
            DateTime lastUpdateTime = DateTime.Now;
            long lastBytesDownloaded = 0;

            progressHandler = (sender, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.DownloadedBytes = e.BytesDownloaded;

                    // 计算速度
                    DateTime now = DateTime.Now;
                    TimeSpan elapsed = now - lastUpdateTime;
                    if (elapsed.TotalSeconds >= 1)
                    {
                        long bytesSinceLastUpdate = e.BytesDownloaded - lastBytesDownloaded;
                        double speed = bytesSinceLastUpdate / elapsed.TotalSeconds;
                        task.Speed = FormatSpeed(speed);

                        // 计算剩余时间
                        if (speed > 0 && e.TotalBytes > e.BytesDownloaded)
                        {
                            double remainingBytes = e.TotalBytes - e.BytesDownloaded;
                            double remainingSeconds = remainingBytes / speed;
                            task.RemainingTime = FormatTime((int)remainingSeconds);
                        }

                        lastBytesDownloaded = e.BytesDownloaded;
                        lastUpdateTime = now;
                    }
                });
            };

            sshService.DownloadProgressChanged += progressHandler;

            try
            {
                var options = new Models.FileTransferOptions
                {
                    LocalPath = task.LocalPath,
                    RemotePath = task.RemotePath,
                    CancellationToken = cancellationTokenSource.Token
                };
                await sshService.DownloadFileWithProgressAsync(options);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.Status = DownloadStatus.Completed;
                    task.CompleteTime = DateTime.Now;
                    task.ProgressPercentage = 100;
                    task.Speed = "0 KB/s";
                    task.RemainingTime = "已完成";
                });

                TaskCompleted?.Invoke(this, task);
            }
            catch (OperationCanceledException)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.Status = DownloadStatus.Cancelled;
                    task.RemainingTime = "已取消";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.Status = DownloadStatus.Failed;
                    task.ErrorMessage = ex.Message;
                    task.RemainingTime = "失败";
                });
            }
            finally
            {
                sshService.DownloadProgressChanged -= progressHandler;
            }
        }

        public void OpenFileLocation(DownloadTask task)
        {
            if (File.Exists(task.LocalPath))
            {
                Process.Start("explorer.exe", $"/select,\"{task.LocalPath}\"");
            }
            else
            {
                MessageBox.Show("文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void OpenFile(DownloadTask task)
        {
            if (File.Exists(task.LocalPath))
            {
                Process.Start(new ProcessStartInfo(task.LocalPath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("文件不存在", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void ClearCompletedTasks()
        {
            var completedTasks = DownloadTasks.Where(t =>
                t.Status == DownloadStatus.Completed ||
                t.Status == DownloadStatus.Failed ||
                t.Status == DownloadStatus.Cancelled).ToList();

            foreach (var task in completedTasks)
            {
                DownloadTasks.Remove(task);
            }
        }

        public void RemoveTask(DownloadTask task)
        {
            DownloadTasks.Remove(task);
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond < 1024)
                return $"{bytesPerSecond:F0} B/s";
            else if (bytesPerSecond < 1024 * 1024)
                return $"{(bytesPerSecond / 1024.0):F2} KB/s";
            else
                return $"{(bytesPerSecond / (1024.0 * 1024.0)):F2} MB/s";
        }

        private static string FormatTime(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}秒";
            else if (seconds < 3600)
                return $"{seconds / 60}分{seconds % 60}秒";
            else
                return $"{seconds / 3600}时{(seconds % 3600) / 60}分";
        }
    }
}
