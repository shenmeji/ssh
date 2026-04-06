using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class DownloadProgressWindow : Window
    {
        private readonly SshService _sshService;
        private readonly string _remotePath;
        private readonly string _localPath;
        private readonly string _fileName;
        private CancellationTokenSource _cancellationTokenSource;
        private long _totalBytes;
        private long _bytesDownloaded;
        private DateTime _startTime;
        private long _lastBytesDownloaded;
        private DateTime _lastUpdateTime;

        public bool IsCanceled { get; private set; } = false;
        public bool IsCompleted { get; private set; } = false;

        public DownloadProgressWindow(SshService sshService, string remotePath, string localPath, string fileName)
        {
            InitializeComponent();
            _sshService = sshService;
            _remotePath = remotePath;
            _localPath = localPath;
            _fileName = fileName;
            _cancellationTokenSource = new CancellationTokenSource();

            FileNameText.Text = $"下载文件: {fileName}";
            ProgressBar.Value = 0;
            ProgressText.Text = "0% - 0 KB / 0 KB";
            SpeedText.Text = "速度: 0 KB/s";
            TimeText.Text = "剩余时间: 计算中...";

            _sshService.DownloadProgressChanged += SshService_DownloadProgressChanged;
        }

        public async Task StartDownloadAsync()
        {
            _startTime = DateTime.Now;
            _lastBytesDownloaded = 0;
            _lastUpdateTime = _startTime;

            try
            {
                var options = new SimpleSshClient.Models.FileTransferOptions
                {
                    LocalPath = _localPath,
                    RemotePath = _remotePath,
                    CancellationToken = _cancellationTokenSource.Token
                };
                await _sshService.DownloadFileWithProgressAsync(options);
                IsCompleted = true;
            }
            catch (Exception ex)
            {
                if (!IsCanceled)
                {
                    MessageBox.Show($"下载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                _sshService.DownloadProgressChanged -= SshService_DownloadProgressChanged;
                Close();
            }
        }

        private void SshService_DownloadProgressChanged(object? sender, DownloadProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _bytesDownloaded = e.BytesDownloaded;
                _totalBytes = e.TotalBytes;

                // 更新进度条
                ProgressBar.Value = e.ProgressPercentage;

                // 更新进度文本
                string downloadedText = FormatSize(_bytesDownloaded);
                string totalText = FormatSize(_totalBytes);
                ProgressText.Text = $"{e.ProgressPercentage:F1}% - {downloadedText} / {totalText}";

                // 计算下载速度
                DateTime now = DateTime.Now;
                TimeSpan elapsed = now - _lastUpdateTime;
                if (elapsed.TotalSeconds >= 1)
                {
                    long bytesSinceLastUpdate = _bytesDownloaded - _lastBytesDownloaded;
                    double speed = bytesSinceLastUpdate / elapsed.TotalSeconds;
                    SpeedText.Text = $"速度: {FormatSize((long)speed)}/s";

                    // 计算剩余时间
                    if (speed > 0 && _totalBytes > _bytesDownloaded)
                    {
                        double remainingBytes = _totalBytes - _bytesDownloaded;
                        double remainingSeconds = remainingBytes / speed;
                        TimeText.Text = $"剩余时间: {FormatTime((int)remainingSeconds)}";
                    }
                    else
                    {
                        TimeText.Text = "剩余时间: 计算中...";
                    }

                    _lastBytesDownloaded = _bytesDownloaded;
                    _lastUpdateTime = now;
                }
            });
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            else if (bytes < 1024 * 1024)
                return $"{(bytes / 1024.0):F2} KB";
            else if (bytes < 1024 * 1024 * 1024)
                return $"{(bytes / (1024.0 * 1024.0)):F2} MB";
            else
                return $"{(bytes / (1024.0 * 1024.0 * 1024.0)):F2} GB";
        }

        private string FormatTime(int seconds)
        {
            if (seconds < 60)
                return $"{seconds}秒";
            else if (seconds < 3600)
                return $"{seconds / 60}分{seconds % 60}秒";
            else
                return $"{seconds / 3600}时{(seconds % 3600) / 60}分";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsCanceled = true;
            _cancellationTokenSource.Cancel();
        }
    }
}