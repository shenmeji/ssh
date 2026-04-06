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


    public class UploadManager
    {
        private static UploadManager? _instance;
        private static readonly object _lock = new();

        public static UploadManager Instance
        {
            get
            {
                lock (_lock)
                {
                    _instance ??= new UploadManager();
                    return _instance;
                }
            }
        }

        public ObservableCollection<UploadTask> UploadTasks { get; } = new();
        public event EventHandler<UploadTask>? TaskAdded;
        public event EventHandler<UploadTask>? TaskCompleted;

        private UploadManager()
        {
        }

        public UploadTask AddUploadTask(string fileName, string localPath, string remotePath, long totalBytes, SshService sshService)
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

            var task = new UploadTask
            {
                FileName = fileName,
                LocalPath = localPath,
                RemotePath = remotePath,
                TotalBytes = totalBytes,
                Status = UploadStatus.Pending,
                StartTime = DateTime.Now,
                ConnectionId = connectionId
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                UploadTasks.Insert(0, task);
            });

            TaskAdded?.Invoke(this, task);

            // 启动上传
            _ = StartUploadAsync(task, sshService);

            return task;
        }

        private async Task StartUploadAsync(UploadTask task, SshService sshService)
        {
            task.Status = UploadStatus.Uploading;
            var cancellationTokenSource = new CancellationTokenSource();

            // 订阅进度事件
            EventHandler<UploadProgressEventArgs>? progressHandler = null;
            DateTime lastUpdateTime = DateTime.Now;
            long lastBytesUploaded = 0;

            progressHandler = (sender, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.UploadedBytes = e.BytesUploaded;

                    // 计算速度
                    DateTime now = DateTime.Now;
                    TimeSpan elapsed = now - lastUpdateTime;
                    if (elapsed.TotalSeconds >= 1)
                    {
                        long bytesSinceLastUpdate = e.BytesUploaded - lastBytesUploaded;
                        double speed = bytesSinceLastUpdate / elapsed.TotalSeconds;
                        task.Speed = FormatSpeed(speed);

                        // 计算剩余时间
                        if (speed > 0 && e.TotalBytes > e.BytesUploaded)
                        {
                            double remainingBytes = e.TotalBytes - e.BytesUploaded;
                            double remainingSeconds = remainingBytes / speed;
                            task.RemainingTime = FormatTime((int)remainingSeconds);
                        }

                        lastBytesUploaded = e.BytesUploaded;
                        lastUpdateTime = now;
                    }
                });
            };

            sshService.UploadProgressChanged += progressHandler;

            try
            {
                // 检查文件是否存在
                try
                {
                    // 尝试上传，如果文件存在会抛出异常
                var options = new SimpleSshClient.Models.FileTransferOptions
                {
                    LocalPath = task.LocalPath,
                    RemotePath = task.RemotePath,
                    CancellationToken = cancellationTokenSource.Token,
                    Overwrite = false
                };
                await sshService.UploadFileWithProgressAsync(options);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("已存在"))
                {
                    // 文件存在，显示确认对话框
                    bool? result = Application.Current.Dispatcher.Invoke(() =>
                    {
                        return MessageBox.Show(
                            $"文件 {task.RemotePath} 已存在，是否覆盖？",
                            "确认覆盖",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        ) == MessageBoxResult.Yes;
                    });

                    if (result == true)
                    {
                        // 用户确认覆盖，重新上传
                        var options = new SimpleSshClient.Models.FileTransferOptions
                        {
                            LocalPath = task.LocalPath,
                            RemotePath = task.RemotePath,
                            CancellationToken = cancellationTokenSource.Token,
                            Overwrite = true
                        };
                        await sshService.UploadFileWithProgressAsync(options);
                    }
                    else
                    {
                        // 用户取消，标记为取消状态
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            task.Status = UploadStatus.Cancelled;
                            task.RemainingTime = "已取消";
                        });
                        return;
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.Status = UploadStatus.Completed;
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
                    task.Status = UploadStatus.Cancelled;
                    task.RemainingTime = "已取消";
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    task.Status = UploadStatus.Failed;
                    task.ErrorMessage = ex.Message;
                    task.RemainingTime = "失败";
                });
            }
            finally
            {
                sshService.UploadProgressChanged -= progressHandler;
            }
        }

        public void OpenFileLocation(UploadTask task)
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

        public void OpenFile(UploadTask task)
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
            var completedTasks = UploadTasks.Where(t =>
                t.Status == UploadStatus.Completed ||
                t.Status == UploadStatus.Failed ||
                t.Status == UploadStatus.Cancelled).ToList();

            foreach (var task in completedTasks)
            {
                UploadTasks.Remove(task);
            }
        }

        public void RemoveTask(UploadTask task)
        {
            UploadTasks.Remove(task);
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
