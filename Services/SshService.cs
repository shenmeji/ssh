using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SimpleSshClient.Models;

namespace SimpleSshClient.Services
{
    public class DownloadProgressEventArgs : EventArgs
    {
        public long BytesDownloaded { get; }
        public long TotalBytes { get; }
        public double ProgressPercentage { get; }

        public DownloadProgressEventArgs(long bytesDownloaded, long totalBytes)
        {
            BytesDownloaded = bytesDownloaded;
            TotalBytes = totalBytes;
            ProgressPercentage = totalBytes > 0 ? (double)bytesDownloaded / totalBytes * 100 : 0;
        }
    }

    public class UploadProgressEventArgs : EventArgs
    {
        public long BytesUploaded { get; }
        public long TotalBytes { get; }
        public double ProgressPercentage { get; }

        public UploadProgressEventArgs(long bytesUploaded, long totalBytes)
        {
            BytesUploaded = bytesUploaded;
            TotalBytes = totalBytes;
            ProgressPercentage = totalBytes > 0 ? (double)bytesUploaded / totalBytes * 100 : 0;
        }
    }

    /// <summary>
    /// SSH服务类，提供SSH连接、命令执行和SFTP文件传输功能
    /// </summary>
    public class SshService : IDisposable
    {
        /// <summary>
        /// SSH客户端实例
        /// </summary>
        private SshClient? _sshClient;
        /// <summary>
        /// Shell流实例，用于交互式命令执行
        /// </summary>
        private ShellStream? _shellStream;
        /// <summary>
        /// SFTP客户端实例，用于文件传输
        /// </summary>
        private SftpClient? _sftpClient;
        /// <summary>
        /// 资源是否已释放
        /// </summary>
        private bool _disposed;
        /// <summary>
        /// 终端列数
        /// </summary>
        private uint _terminalColumns = 120;
        /// <summary>
        /// 终端行数
        /// </summary>
        private uint _terminalRows = 40;
        /// <summary>
        /// 输出接收事件
        /// </summary>
        public event EventHandler<string>? OutputReceived;
        /// <summary>
        /// 连接关闭事件
        /// </summary>
        public event EventHandler? ConnectionClosed;
        /// <summary>
        /// 下载进度变更事件
        /// </summary>
        public event EventHandler<DownloadProgressEventArgs>? DownloadProgressChanged;
        /// <summary>
        /// 上传进度变更事件
        /// </summary>
        public event EventHandler<UploadProgressEventArgs>? UploadProgressChanged;

        /// <summary>
        /// 获取SSH连接状态
        /// </summary>
        public bool IsConnected => _sshClient?.IsConnected ?? false;

        /// <summary>
        /// 获取SFTP连接状态
        /// </summary>
        public bool IsSftpConnected => _sftpClient?.IsConnected ?? false;

        /// <summary>
        /// 异步连接到SSH服务器
        /// </summary>
        /// <param name="connectionInfo">连接信息</param>
        /// <exception cref="Exception">连接失败时抛出异常</exception>
        public async Task ConnectAsync(Models.ConnectionInfo connectionInfo)
        {
            const int maxRetries = 3;
            int retryCount = 0;
            Exception? lastException = null;

            while (retryCount < maxRetries)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        // 从连接池获取连接
                        _sshClient = ConnectionPool.Instance.GetConnection(connectionInfo);
                        
                        // 创建shell通道
                        _shellStream = _sshClient.CreateShellStream("xterm-256color", _terminalColumns, _terminalRows, _terminalColumns * 8, _terminalRows * 16, 4096);
                        _shellStream.DataReceived += ShellStream_DataReceived;

                        // 使用同一个连接创建SFTP客户端
                        _sftpClient = new SftpClient(_sshClient.ConnectionInfo);
                        _sftpClient.Connect();
                    });
                    return; // 连接成功，退出循环
                }
                catch (Renci.SshNet.Common.SshConnectionException ex) when (retryCount < maxRetries - 1)
                {
                    lastException = ex;
                    retryCount++;
                    // 等待一段时间后重试
                    await Task.Delay(1000 * retryCount);
                }
                catch (Renci.SshNet.Common.SshAuthenticationException ex)
                {
                    throw new Exception($"认证失败，请检查用户名和密码。\n服务器: {connectionInfo.Host}:{connectionInfo.Port}\n用户名: {connectionInfo.Username}\n错误: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    throw new Exception($"连接过程中发生错误。\n服务器: {connectionInfo.Host}:{connectionInfo.Port}\n错误: {ex.Message}", ex);
                }
            }

            // 所有重试都失败
            if (lastException != null)
            {
                throw new Exception($"SSH连接失败，请检查网络连接和服务器设置。\n服务器: {connectionInfo.Host}:{connectionInfo.Port}\n错误: {lastException.Message}", lastException);
            }
        }

        /// <summary>
        /// 调整终端大小
        /// </summary>
        /// <param name="columns">列数</param>
        /// <param name="rows">行数</param>
        public void ResizeTerminal(uint columns, uint rows)
        {
            if (_shellStream != null && _sshClient != null && _sshClient.IsConnected)
            {
                _terminalColumns = columns;
                _terminalRows = rows;
            }
        }

        private string _lastOutput = string.Empty;
        private int _repeatedOutputCount = 0;
        private const int MAX_REPEATED_OUTPUT = 10;

        private void ShellStream_DataReceived(object? sender, Renci.SshNet.Common.ShellDataEventArgs e)
        {
            var output = Encoding.UTF8.GetString(e.Data);
            
            // 过滤重复的输出，避免无限循环
            if (!string.IsNullOrEmpty(output))
            {
                if (output == _lastOutput)
                {
                    _repeatedOutputCount++;
                    if (_repeatedOutputCount < MAX_REPEATED_OUTPUT)
                    {
                        OutputReceived?.Invoke(this, output);
                    }
                    else if (_repeatedOutputCount == MAX_REPEATED_OUTPUT)
                    {
                        // 显示重复输出的提示
                        OutputReceived?.Invoke(this, "[重复输出已省略]\n");
                    }
                }
                else
                {
                    _lastOutput = output;
                    _repeatedOutputCount = 0;
                    OutputReceived?.Invoke(this, output);
                }
            }
        }

        /// <summary>
        /// 发送命令到SSH服务器
        /// </summary>
        /// <param name="command">要执行的命令</param>
        public void SendCommand(string command)
        {
            if (_shellStream != null && _shellStream.CanWrite)
            {
                _shellStream.WriteLine(command);
            }
        }

        /// <summary>
        /// 发送中断信号到SSH服务器（相当于按下Ctrl+C）
        /// </summary>
        public void SendInterrupt()
        {
            if (_shellStream != null && _shellStream.CanWrite)
            {
                _shellStream.WriteByte(0x03);
                _shellStream.Flush();
            }
        }

        /// <summary>
        /// 异步执行命令并返回结果
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <returns>命令执行结果</returns>
        /// <exception cref="InvalidOperationException">未连接到服务器时抛出</exception>
        public async Task<string> RunCommandAsync(string command)
        {
            if (_sshClient == null || !_sshClient.IsConnected)
                throw new InvalidOperationException("未连接到服务器");

            return await Task.Run(() =>
            {
                var cmd = _sshClient.CreateCommand(command);
                return cmd.Execute();
            });
        }

        /// <summary>
        /// 异步获取服务器主机名
        /// </summary>
        /// <returns>服务器主机名</returns>
        public async Task<string> GetHostnameAsync()
        {
            try
            {
                var hostname = await RunCommandAsync("hostname");
                return hostname.Trim();
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// 异步上传文件到服务器
        /// </summary>
        /// <param name="localPath">本地文件路径</param>
        /// <param name="remotePath">远程文件路径</param>
        public async Task UploadFileAsync(string localPath, string remotePath)
        {
            var options = new Models.FileTransferOptions
            {
                LocalPath = localPath,
                RemotePath = remotePath
            };
            await UploadFileAsync(options);
        }

        /// <summary>
        /// 异步上传文件到服务器（支持高级选项）
        /// </summary>
        /// <param name="options">文件传输选项</param>
        /// <exception cref="InvalidOperationException">SFTP客户端未连接时抛出</exception>
        public async Task UploadFileAsync(Models.FileTransferOptions options)
        {
            await Task.Run(() =>
            {
                if (_sftpClient == null || !_sftpClient.IsConnected)
                    throw new InvalidOperationException("SFTP client not connected");

                // 检查目标文件是否存在
                if (!options.Overwrite && _sftpClient.Exists(options.RemotePath))
                {
                    throw new InvalidOperationException($"文件 {options.RemotePath} 已存在");
                }

                using var fileStream = File.OpenRead(options.LocalPath);
                long fileSize = fileStream.Length;
                long totalBytesWritten = 0;
                
                // 分块传输设置
                const long CHUNK_SIZE = 1024 * 1024; // 1MB分块
                long remainingBytes = fileSize;
                
                // 动态缓冲区大小设置
                int minBufferSize = 4096;    // 最小缓冲区
                int maxBufferSize = 65536;   // 最大缓冲区
                int currentBufferSize = 8192; // 初始缓冲区
                
                // 性能监控
                DateTime lastSpeedCheck = DateTime.Now;
                long bytesSinceLastCheck = 0;
                const int speedCheckInterval = 1000; // 1秒检查一次

                // 自定义上传方法，支持进度回调
                using var remoteStream = _sftpClient.Create(options.RemotePath);
                
                while (remainingBytes > 0)
                {
                    if (options.CancellationToken.IsCancellationRequested)
                        break;
                    
                    // 计算当前块的大小
                    long chunkSize = Math.Min(CHUNK_SIZE, remainingBytes);
                    byte[] buffer = new byte[Math.Min(currentBufferSize, (int)chunkSize)];
                    long bytesWrittenInChunk = 0;
                    
                    while (bytesWrittenInChunk < chunkSize)
                    {
                        int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;
                        
                        remoteStream.Write(buffer, 0, bytesRead);
                        totalBytesWritten += bytesRead;
                        bytesWrittenInChunk += bytesRead;
                        remainingBytes -= bytesRead;
                        bytesSinceLastCheck += bytesRead;
                        
                        // 计算传输速度并调整缓冲区大小
                        TimeSpan elapsed = DateTime.Now - lastSpeedCheck;
                        if (elapsed.TotalMilliseconds >= speedCheckInterval)
                        {
                            double speed = bytesSinceLastCheck / elapsed.TotalSeconds / 1024; // KB/s
                            currentBufferSize = AdjustBufferSize(currentBufferSize, speed, minBufferSize, maxBufferSize);
                            
                            lastSpeedCheck = DateTime.Now;
                            bytesSinceLastCheck = 0;
                        }
                        
                        // 调用进度回调
                        if (options.ProgressCallback != null && fileSize > 0)
                        {
                            double progress = (double)totalBytesWritten / fileSize;
                            options.ProgressCallback(progress);
                        }
                    }
                }
            }, options.CancellationToken);
        }

        /// <summary>
        /// 异步上传文件到服务器（带进度事件）
        /// </summary>
        /// <param name="options">文件传输选项</param>
        /// <exception cref="InvalidOperationException">SFTP客户端未连接时抛出</exception>
        public async Task UploadFileWithProgressAsync(Models.FileTransferOptions options)
        {
            await Task.Run(() =>
            {
                if (_sftpClient == null || !_sftpClient.IsConnected)
                    throw new InvalidOperationException("SFTP client not connected");

                // 检查目标文件是否存在
                if (!options.Overwrite && _sftpClient.Exists(options.RemotePath))
                {
                    throw new InvalidOperationException($"文件 {options.RemotePath} 已存在");
                }

                using var fileStream = File.OpenRead(options.LocalPath);
                long fileSize = fileStream.Length;
                long totalBytesWritten = 0;
                
                // 分块传输设置
                const long CHUNK_SIZE = 1024 * 1024; // 1MB分块
                long remainingBytes = fileSize;
                
                // 动态缓冲区大小设置
                int minBufferSize = 4096;    // 最小缓冲区
                int maxBufferSize = 65536;   // 最大缓冲区
                int currentBufferSize = 8192; // 初始缓冲区
                
                // 性能监控
                DateTime lastSpeedCheck = DateTime.Now;
                long bytesSinceLastCheck = 0;
                const int speedCheckInterval = 1000; // 1秒检查一次

                // 计算进度更新间隔（每1%或至少每100KB）
                long progressInterval = fileSize > 0 ? Math.Max(fileSize / 100, 1024) : 102400;
                long lastReportedBytes = 0;

                using var remoteStream = _sftpClient.Create(options.RemotePath);
                
                while (remainingBytes > 0)
                {
                    if (options.CancellationToken.IsCancellationRequested)
                        break;
                    
                    // 计算当前块的大小
                    long chunkSize = Math.Min(CHUNK_SIZE, remainingBytes);
                    byte[] buffer = new byte[Math.Min(currentBufferSize, (int)chunkSize)];
                    long bytesWrittenInChunk = 0;
                    
                    while (bytesWrittenInChunk < chunkSize)
                    {
                        int bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;
                        
                        remoteStream.Write(buffer, 0, bytesRead);
                        totalBytesWritten += bytesRead;
                        bytesWrittenInChunk += bytesRead;
                        remainingBytes -= bytesRead;
                        bytesSinceLastCheck += bytesRead;
                        
                        // 计算传输速度并调整缓冲区大小
                        TimeSpan elapsed = DateTime.Now - lastSpeedCheck;
                        if (elapsed.TotalMilliseconds >= speedCheckInterval)
                        {
                            double speed = bytesSinceLastCheck / elapsed.TotalSeconds / 1024; // KB/s
                            currentBufferSize = AdjustBufferSize(currentBufferSize, speed, minBufferSize, maxBufferSize);
                            
                            lastSpeedCheck = DateTime.Now;
                            bytesSinceLastCheck = 0;
                        }

                        // 每上传一定进度更新一次（避免小文件除以零）
                        if (totalBytesWritten - lastReportedBytes >= progressInterval)
                        {
                            UploadProgressChanged?.Invoke(this, new UploadProgressEventArgs(totalBytesWritten, fileSize));
                            lastReportedBytes = totalBytesWritten;
                        }
                    }
                }

                // 确保最后一次更新
                UploadProgressChanged?.Invoke(this, new UploadProgressEventArgs(totalBytesWritten, fileSize));
            }, options.CancellationToken);
        }

        /// <summary>
        /// 异步从服务器下载文件
        /// </summary>
        /// <param name="remotePath">远程文件路径</param>
        /// <param name="localPath">本地文件路径</param>
        public async Task DownloadFileAsync(string remotePath, string localPath)
        {
            var options = new Models.FileTransferOptions
            {
                LocalPath = localPath,
                RemotePath = remotePath
            };
            await DownloadFileAsync(options);
        }

        /// <summary>
        /// 异步从服务器下载文件（支持高级选项）
        /// </summary>
        /// <param name="options">文件传输选项</param>
        public async Task DownloadFileAsync(Models.FileTransferOptions options)
        {
            await Task.Run(() =>
            {
                using var fileStream = File.Create(options.LocalPath);
                _sftpClient?.DownloadFile(options.RemotePath, fileStream);
            }, options.CancellationToken);
        }

        /// <summary>
        /// 异步从服务器下载文件（带进度事件）
        /// </summary>
        /// <param name="options">文件传输选项</param>
        /// <exception cref="InvalidOperationException">SFTP客户端未连接时抛出</exception>
        public async Task DownloadFileWithProgressAsync(Models.FileTransferOptions options)
        {
            await Task.Run(() =>
            {
                if (_sftpClient == null || !_sftpClient.IsConnected)
                    throw new InvalidOperationException("SFTP client not connected");

                // 获取文件大小
                var fileInfo = _sftpClient.Get(options.RemotePath);
                long fileSize = fileInfo.Length;
                long totalBytesRead = 0;
                
                // 分块传输设置
                const long CHUNK_SIZE = 1024 * 1024; // 1MB分块
                long remainingBytes = fileSize;
                
                // 动态缓冲区大小设置
                int minBufferSize = 4096;    // 最小缓冲区
                int maxBufferSize = 65536;   // 最大缓冲区
                int currentBufferSize = 8192; // 初始缓冲区
                
                // 性能监控
                DateTime lastSpeedCheck = DateTime.Now;
                long bytesSinceLastCheck = 0;
                const int speedCheckInterval = 1000; // 1秒检查一次

                using var fileStream = File.Create(options.LocalPath);
                using var sftpStream = _sftpClient.OpenRead(options.RemotePath);

                // 计算进度更新间隔（每1%或至少每100KB）
                long progressInterval = fileSize > 0 ? Math.Max(fileSize / 100, 1024) : 102400;
                long lastReportedBytes = 0;

                while (remainingBytes > 0)
                {
                    if (options.CancellationToken.IsCancellationRequested)
                        break;
                    
                    // 计算当前块的大小
                    long chunkSize = Math.Min(CHUNK_SIZE, remainingBytes);
                    byte[] buffer = new byte[Math.Min(currentBufferSize, (int)chunkSize)];
                    long bytesReadInChunk = 0;
                    
                    while (bytesReadInChunk < chunkSize)
                    {
                        int bytesRead = sftpStream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;
                        
                        fileStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        bytesReadInChunk += bytesRead;
                        remainingBytes -= bytesRead;
                        bytesSinceLastCheck += bytesRead;
                        
                        // 计算传输速度并调整缓冲区大小
                        TimeSpan elapsed = DateTime.Now - lastSpeedCheck;
                        if (elapsed.TotalMilliseconds >= speedCheckInterval)
                        {
                            double speed = bytesSinceLastCheck / elapsed.TotalSeconds / 1024; // KB/s
                            currentBufferSize = AdjustBufferSize(currentBufferSize, speed, minBufferSize, maxBufferSize);
                            
                            lastSpeedCheck = DateTime.Now;
                            bytesSinceLastCheck = 0;
                        }

                        // 每下载一定进度更新一次（避免小文件除以零）
                        if (totalBytesRead - lastReportedBytes >= progressInterval)
                        {
                            DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs(totalBytesRead, fileSize));
                            lastReportedBytes = totalBytesRead;
                        }
                    }
                }

                // 确保最后一次更新
                DownloadProgressChanged?.Invoke(this, new DownloadProgressEventArgs(totalBytesRead, fileSize));
            }, options.CancellationToken);
        }

        /// <summary>
        /// 根据传输速度动态调整缓冲区大小
        /// </summary>
        /// <param name="currentSize">当前缓冲区大小</param>
        /// <param name="speed">当前传输速度（KB/s）</param>
        /// <param name="minSize">最小缓冲区大小</param>
        /// <param name="maxSize">最大缓冲区大小</param>
        /// <returns>调整后的缓冲区大小</returns>
        private int AdjustBufferSize(int currentSize, double speed, int minSize, int maxSize)
        {
            // 根据传输速度调整缓冲区大小
            if (speed > 1000) // 高速网络
            {
                return Math.Min(currentSize * 2, maxSize);
            }
            else if (speed > 500) // 中高速网络
            {
                return Math.Min((int)(currentSize * 1.5), maxSize);
            }
            else if (speed < 100) // 低速网络
            {
                return Math.Max(currentSize / 2, minSize);
            }
            else if (speed < 50) // 极低速网络
            {
                return Math.Max(currentSize / 4, minSize);
            }
            // 中等速度网络，保持当前大小
            return currentSize;
        }

        /// <summary>
        /// 异步删除服务器上的文件
        /// </summary>
        /// <param name="path">文件路径</param>
        public async Task DeleteFileAsync(string path)
        {
            await Task.Run(() =>
            {
                _sftpClient?.DeleteFile(path);
            });
        }

        /// <summary>
        /// 异步重命名服务器上的文件
        /// </summary>
        /// <param name="oldPath">旧文件路径</param>
        /// <param name="newPath">新文件路径</param>
        public async Task RenameFileAsync(string oldPath, string newPath)
        {
            await Task.Run(() =>
            {
                _sftpClient?.RenameFile(oldPath, newPath);
            });
        }

        /// <summary>
        /// 异步复制服务器上的文件或文件夹
        /// </summary>
        /// <param name="sourcePath">源路径</param>
        /// <param name="destinationPath">目标路径</param>
        /// <exception cref="InvalidOperationException">SFTP客户端未连接时抛出</exception>
        public async Task CopyFileAsync(string sourcePath, string destinationPath)
        {
            await Task.Run(() =>
            {
                if (_sftpClient == null || !_sftpClient.IsConnected)
                    throw new InvalidOperationException("SFTP client not connected");

                // 检查源路径是否为文件夹
                var sourceFileInfo = _sftpClient.Get(sourcePath);
                if (sourceFileInfo.IsDirectory)
                {
                    // 检查目标文件夹是否是源文件夹的子文件夹
                    if (IsSubdirectory(sourcePath, destinationPath))
                    {
                        throw new InvalidOperationException("目标文件夹是源文件夹的子文件夹，无法复制");
                    }
                    // 递归复制文件夹
                    CopyDirectory(sourcePath, destinationPath);
                }
                else
                {
                    // 复制文件
                    using var sourceStream = _sftpClient.OpenRead(sourcePath);
                    using var destinationStream = _sftpClient.Create(destinationPath);
                    sourceStream.CopyTo(destinationStream);
                }
            });
        }

        /// <summary>
        /// 递归复制文件夹
        /// </summary>
        /// <param name="sourcePath">源文件夹路径</param>
        /// <param name="destinationPath">目标文件夹路径</param>
        /// <exception cref="InvalidOperationException">SFTP客户端未连接时抛出</exception>
        private void CopyDirectory(string sourcePath, string destinationPath)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                throw new InvalidOperationException("SFTP client not connected");

            // 检查目标文件夹是否是源文件夹的子文件夹
            if (IsSubdirectory(sourcePath, destinationPath))
            {
                throw new InvalidOperationException("目标文件夹是源文件夹的子文件夹，无法复制");
            }

            // 创建目标文件夹
            if (!_sftpClient.Exists(destinationPath))
            {
                _sftpClient.CreateDirectory(destinationPath);
            }

            // 复制文件夹内的所有文件和子文件夹
            var files = _sftpClient.ListDirectory(sourcePath);
            foreach (var file in files)
            {
                if (file.Name == "." || file.Name == "..")
                    continue;

                var sourceFilePath = System.IO.Path.Combine(sourcePath, file.Name).Replace('\\', '/');
                var destinationFilePath = System.IO.Path.Combine(destinationPath, file.Name).Replace('\\', '/');

                if (file.IsDirectory)
                {
                    // 检查目标子文件夹是否是源子文件夹的子文件夹
                    if (IsSubdirectory(sourceFilePath, destinationFilePath))
                    {
                        throw new InvalidOperationException("目标文件夹是源文件夹的子文件夹，无法复制");
                    }
                    // 递归复制子文件夹
                    CopyDirectory(sourceFilePath, destinationFilePath);
                }
                else
                {
                    // 复制文件
                    using var sourceStream = _sftpClient.OpenRead(sourceFilePath);
                    using var destinationStream = _sftpClient.Create(destinationFilePath);
                    sourceStream.CopyTo(destinationStream);
                }
            }
        }

        /// <summary>
        /// 检查目标路径是否是源路径的子目录
        /// </summary>
        /// <param name="sourcePath">源路径</param>
        /// <param name="destinationPath">目标路径</param>
        /// <returns>如果目标路径是源路径的子目录，返回true；否则返回false</returns>
        private bool IsSubdirectory(string sourcePath, string destinationPath)
        {
            // 确保路径以/结尾，以便正确比较
            string normalizedSource = sourcePath.TrimEnd('/') + "/";
            string normalizedDestination = destinationPath.TrimEnd('/') + "/";
            
            // 检查目标路径是否以源路径开头
            return normalizedDestination.StartsWith(normalizedSource);
        }

        /// <summary>
        /// 异步创建服务器上的目录
        /// </summary>
        /// <param name="path">目录路径</param>
        public async Task CreateDirectoryAsync(string path)
        {
            await Task.Run(() =>
            {
                _sftpClient?.CreateDirectory(path);
            });
        }

        /// <summary>
        /// 异步删除服务器上的目录
        /// </summary>
        /// <param name="path">目录路径</param>
        public async Task DeleteDirectoryAsync(string path)
        {
            await Task.Run(() =>
            {
                _sftpClient?.DeleteDirectory(path);
            });
        }

        /// <summary>
        /// 异步列出服务器上的目录内容
        /// </summary>
        /// <param name="path">目录路径</param>
        /// <returns>目录内容列表</returns>
        public async Task<List<ISftpFile>> ListDirectoryAsync(string path)
        {
            return await Task.Run(() =>
            {
                return _sftpClient?.ListDirectory(path).ToList() ?? new List<ISftpFile>();
            });
        }

        /// <summary>
        /// 获取服务器上文件的信息
        /// </summary>
        /// <param name="path">文件路径</param>
        /// <returns>文件信息，如不存在返回null</returns>
        public ISftpFile? GetFileInfo(string path)
        {
            if (_sftpClient == null || !_sftpClient.IsConnected)
                return null;

            try
            {
                return _sftpClient.Get(path);
            }
            catch
            {
                return null;
            }
        }



        /// <summary>
        /// 断开SSH连接
        /// </summary>
        public void Disconnect()
        {
            if (_shellStream != null)
            {
                try
                {
                    _shellStream.DataReceived -= ShellStream_DataReceived;
                    _shellStream.Close();
                    _shellStream.Dispose();
                }
                catch { }
                _shellStream = null;
            }

            if (_sftpClient != null)
            {
                try
                {
                    _sftpClient.Disconnect();
                    _sftpClient.Dispose();
                }
                catch { }
                _sftpClient = null;
            }

            if (_sshClient != null)
            {
                // 释放连接回连接池
                ConnectionPool.Instance.ReleaseConnection(_sshClient);
                _sshClient = null;
            }

            ConnectionClosed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    Disconnect();
                }
                catch { }
                _disposed = true;
            }
        }
    }
}
