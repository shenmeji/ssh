using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleSshClient.Models
{
    public enum UploadStatus
    {
        Pending,
        Uploading,
        Completed,
        Failed,
        Cancelled
    }

    public class UploadTask : INotifyPropertyChanged
    {
        private string _fileName = string.Empty;
        private string _localPath = string.Empty;
        private string _remotePath = string.Empty;
        private long _totalBytes;
        private long _uploadedBytes;
        private double _progressPercentage;
        private UploadStatus _status;
        private string _speed = "0 KB/s";
        private string _remainingTime = "计算中...";
        private string? _errorMessage;
        private DateTime _startTime;
        private DateTime? _completeTime;
        private string _connectionId = string.Empty;

        public string Id { get; } = Guid.NewGuid().ToString("N")[..8];

        public string ConnectionId
        {
            get => _connectionId;
            set { _connectionId = value; OnPropertyChanged(); }
        }

        public string FileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(); }
        }

        public string LocalPath
        {
            get => _localPath;
            set { _localPath = value; OnPropertyChanged(); }
        }

        public string RemotePath
        {
            get => _remotePath;
            set { _remotePath = value; OnPropertyChanged(); }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set { _totalBytes = value; OnPropertyChanged(); }
        }

        public long UploadedBytes
        {
            get => _uploadedBytes;
            set
            {
                _uploadedBytes = value;
                OnPropertyChanged();
                ProgressPercentage = TotalBytes > 0 ? (double)_uploadedBytes / TotalBytes * 100 : 0;
            }
        }

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set { _progressPercentage = value; OnPropertyChanged(); }
        }

        public UploadStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public string Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(); }
        }

        public string RemainingTime
        {
            get => _remainingTime;
            set { _remainingTime = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(); }
        }

        public DateTime? CompleteTime
        {
            get => _completeTime;
            set { _completeTime = value; OnPropertyChanged(); }
        }

        public string FormattedTotalSize => FormatSize(TotalBytes);
        public string FormattedUploadedSize => FormatSize(UploadedBytes);

        public string StatusText => Status switch
        {
            UploadStatus.Pending => "等待中",
            UploadStatus.Uploading => "上传中",
            UploadStatus.Completed => "已完成",
            UploadStatus.Failed => "失败",
            UploadStatus.Cancelled => "已取消",
            _ => "未知"
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string FormatSize(long bytes)
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
    }
}
