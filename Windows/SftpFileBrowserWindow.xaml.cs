using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Renci.SshNet.Sftp;
using SimpleSshClient.Models;
using SimpleSshClient.Services;

namespace SimpleSshClient.Windows
{
    public partial class SftpFileBrowserWindow : Window
    {
        private SshService? _sshService;
        private string _currentPath = "/";
        private readonly Stack<string> _pathHistory = new();
        private string _copiedFilePath = string.Empty;
        private bool _isCutOperation = false;

        public SftpFileBrowserWindow()
        {
            InitializeComponent();
            Loaded += SftpFileBrowserWindow_Loaded;
            ConnectionManagerService.Instance.ConnectionsChanged += OnConnectionsChanged;
        }

        public SftpFileBrowserWindow(SshService sshService)
        {
            InitializeComponent();
            _sshService = sshService;
            Loaded += SftpFileBrowserWindow_Loaded;
            ConnectionManagerService.Instance.ConnectionsChanged += OnConnectionsChanged;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            // 移除事件订阅，避免内存泄漏
            ConnectionManagerService.Instance.ConnectionsChanged -= OnConnectionsChanged;
        }

        private void UpdateStatusText(string message)
        {
            StatusText.Text = message;
        }

        private async void SftpFileBrowserWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadFilesAsync(_currentPath);
            UpdateConnectionComboBox();
        }

        private void OnConnectionsChanged(object sender, EventArgs e)
        {
            UpdateConnectionComboBox();
        }

        private void UpdateConnectionComboBox()
        {
            var storageService = new ConnectionStorageService();
            var savedConnections = storageService.LoadConnections();
            var currentConnections = ConnectionManagerService.Instance.GetConnections();
            var currentSshServices = ConnectionManagerService.Instance.GetSshServices();
            
            ConnectionComboBox.Items.Clear();
            
            foreach (var connection in savedConnections)
            {
                // 检查是否已经有对应的SshService实例
                var existingIndex = currentConnections.FindIndex(c => c.Id == connection.Id);
                SshService sshService;
                
                if (existingIndex >= 0)
                {
                    // 使用现有的SshService实例
                    sshService = currentSshServices[existingIndex];
                }
                else
                {
                    // 创建新的SshService实例
                    sshService = new SshService();
                }
                
                ConnectionComboBox.Items.Add(new ComboBoxItem
                {
                    Content = $"{connection.Name} ({connection.Host}:{connection.Port})",
                    Tag = new Tuple<ConnectionInfo, SshService>(connection, sshService)
                });
                
                // 选择当前使用的连接
                if (_sshService != null && existingIndex >= 0 && currentSshServices[existingIndex] == _sshService)
                {
                    ConnectionComboBox.SelectedIndex = ConnectionComboBox.Items.Count - 1;
                }
            }
        }

        private async void ConnectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConnectionComboBox.SelectedItem is ComboBoxItem item && item.Tag is Tuple<ConnectionInfo, SshService> connectionInfo)
            {
                var connection = connectionInfo.Item1;
                var sshService = connectionInfo.Item2;
                
                try
                {
                    UpdateStatusText("连接中...");
                    
                    // 尝试连接
                    await sshService.ConnectAsync(connection);
                    
                    // 检查是否已经在连接管理器中
                    var currentSshServices = ConnectionManagerService.Instance.GetSshServices();
                    var existingIndex = currentSshServices.IndexOf(sshService);
                    
                    if (existingIndex < 0)
                    {
                        // 添加到连接管理器
                        ConnectionManagerService.Instance.AddConnection(connection, sshService);
                    }
                    else
                    {
                        // 更新最后使用时间
                        ConnectionManagerService.Instance.UpdateLastUsedTime(sshService);
                    }
                    
                    _sshService = sshService;
                    _currentPath = "/";
                    _pathHistory.Clear();
                    await LoadFilesAsync("/");
                    
                    UpdateStatusText("连接成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    UpdateStatusText("连接失败");
                }
            }
        }

        public void UpdateSshService(SshService sshService)
        {
            _sshService = sshService;
            _currentPath = "/";
            _pathHistory.Clear();
            UpdateConnectionComboBox();
        }



        private async System.Threading.Tasks.Task LoadFilesAsync(string path)
        {
            try
            {
                if (_sshService == null)
                {
                    UpdateStatusText("请选择一个连接");
                    return;
                }
                
                // 更新当前路径
                _currentPath = path;
                
                UpdateStatusText("加载中...");
                var files = await _sshService.ListDirectoryAsync(path);
                
                var fileItems = new List<SftpFileItem>();
                
                // 添加上级目录
                if (path != "/")
                {
                    var parentPath = Path.GetDirectoryName(path) ?? "/";
                    // 确保路径格式正确，使用正斜杠
                    parentPath = parentPath.Replace('\\', '/');
                    if (string.IsNullOrEmpty(parentPath))
                    {
                        parentPath = "/";
                    }
                    fileItems.Add(new SftpFileItem
                    {
                        Name = "..",
                        Path = parentPath,
                        Type = "目录",
                        IsDirectory = true
                    });
                }
                
                // 添加当前目录的文件
                foreach (var file in files)
                {
                    if (file.Name == "." || file.Name == "..")
                        continue;

                    var sftpFile = file as Renci.SshNet.Sftp.SftpFile;
                    fileItems.Add(new SftpFileItem
                    {
                        Name = file.Name,
                        Path = Path.Combine(path, file.Name).Replace('\\', '/'),
                        Type = file.IsDirectory ? "目录" : "文件",
                        Size = file.IsDirectory ? "-" : FormatSize(file.Length),
                        LastWriteTime = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        Permissions = "-", // SSH.NET中的权限获取需要通过Attributes
                        IsDirectory = file.IsDirectory
                    });
                }
                
                FilesListView.ItemsSource = fileItems;
                PathTextBox.Text = path;
                UpdateStatusText($"共 {fileItems.Count} 个项目");
                ConnectionManagerService.Instance.UpdateLastUsedTime(_sshService);
            }
            catch (Renci.SshNet.Common.SshConnectionException ex)
            {
                UpdateStatusText($"连接失败: {ex.Message}\n请检查网络连接和服务器设置");
            }
            catch (Renci.SshNet.Common.SshAuthenticationException ex)
            {
                UpdateStatusText($"认证失败: {ex.Message}\n请检查用户名和密码");
            }
            catch (Renci.SshNet.Common.SftpPermissionDeniedException ex)
            {
                UpdateStatusText($"权限不足: {ex.Message}\n请检查您的用户权限");
            }
            catch (Exception ex)
            {
                UpdateStatusText($"加载失败: {ex.Message}");
            }
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

        private void FilesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            bool hasSelection = FilesListView.SelectedItem != null;
            bool isSpecialItem = FilesListView.SelectedItem is SftpFileItem item && item.Name == "..";
            
            // 启用/禁用操作按钮，特殊项目（如".."）不允许操作
            BtnDownload.IsEnabled = hasSelection && !isSpecialItem;
            BtnDelete.IsEnabled = hasSelection && !isSpecialItem;
            BtnRename.IsEnabled = hasSelection && !isSpecialItem;
            BtnEdit.IsEnabled = hasSelection && !isSpecialItem && FilesListView.SelectedItem is SftpFileItem fileItem && !fileItem.IsDirectory;
            BtnCopy.IsEnabled = hasSelection && !isSpecialItem;
            BtnCut.IsEnabled = hasSelection && !isSpecialItem;
            BtnPaste.IsEnabled = !string.IsNullOrEmpty(_copiedFilePath);
        }

        private async void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPath == "/")
            {
                UpdateStatusText("已是根目录");
                return;
            }
            
            // 计算上一级路径
            var parentPath = Path.GetDirectoryName(_currentPath) ?? "/";
            // 确保路径格式正确
            parentPath = parentPath.Replace('\\', '/');
            if (string.IsNullOrEmpty(parentPath))
            {
                parentPath = "/";
            }
            
            await LoadFilesAsync(parentPath);
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is SftpFileItem item)
            {
                _copiedFilePath = item.Path;
                _isCutOperation = false;
                UpdateStatusText($"已复制: {item.Name}");
                BtnPaste.IsEnabled = true;
            }
        }

        private void BtnCut_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is SftpFileItem item)
            {
                _copiedFilePath = item.Path;
                _isCutOperation = true;
                UpdateStatusText($"已剪切: {item.Name}");
                BtnPaste.IsEnabled = true;
            }
        }

        private async void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_copiedFilePath))
                return;

            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }

            try
            {
                var fileName = System.IO.Path.GetFileName(_copiedFilePath);
                var destinationPath = System.IO.Path.Combine(_currentPath, fileName).Replace('\\', '/');

                // 检查目标路径是否存在
                var fileInfo = _sshService.GetFileInfo(destinationPath);

                if (fileInfo != null)
                {
                    // 询问用户是否覆盖
                    var result = MessageBox.Show(
                        $"目标路径 '{fileName}' 已存在，是否覆盖？",
                        "确认覆盖",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        UpdateStatusText("粘贴操作已取消");
                        return;
                    }
                }

                if (_isCutOperation)
                {
                    // 执行剪切操作（重命名）
                    await _sshService.RenameFileAsync(_copiedFilePath, destinationPath);
                    UpdateStatusText($"已剪切到: {fileName}");
                }
                else
                {
                    // 执行复制操作
                    await _sshService.CopyFileAsync(_copiedFilePath, destinationPath);
                    UpdateStatusText($"已复制到: {fileName}");
                }

                // 清空复制/剪切状态
                _copiedFilePath = string.Empty;
                _isCutOperation = false;
                BtnPaste.IsEnabled = false;

                // 刷新文件列表
                await LoadFilesAsync(_currentPath);
            }
            catch (Exception ex)
            {
                UpdateStatusText($"粘贴失败: {ex.Message}");
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadFilesAsync(_currentPath);
        }

        private async void FilesListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (FilesListView.SelectedItem is SftpFileItem item && item.IsDirectory)
            {
                UpdateStatusText($"添加路径到历史记录: {_currentPath}");
                _pathHistory.Push(_currentPath);
                _currentPath = item.Path;
                UpdateStatusText($"导航到: {_currentPath}，历史路径数量: {_pathHistory.Count}");
                await LoadFilesAsync(_currentPath);
            }
        }



        private void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }
            
            var openDialog = new OpenFileDialog
            {
                Title = "选择要上传的文件",
                Multiselect = true
            };

            if (openDialog.ShowDialog() == true)
            {
                foreach (var file in openDialog.FileNames)
                {
                    var fileName = Path.GetFileName(file);
                    var remotePath = Path.Combine(_currentPath, fileName).Replace('\\', '/');
                    
                    // 获取文件大小
                    long fileSize = 0;
                    try
                    {
                        fileSize = new FileInfo(file).Length;
                    }
                    catch { }

                    // 添加到上传管理器（非阻塞）
                    UploadManager.Instance.AddUploadTask(fileName, file, remotePath, fileSize, _sshService);
                }
                
                UpdateStatusText("已添加到上传队列");
            }
        }

        private void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is not SftpFileItem item || item.IsDirectory)
                return;

            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Title = "保存文件",
                FileName = item.Name,
                AddExtension = true
            };

            if (saveDialog.ShowDialog() == true)
            {
                // 获取文件大小
                long fileSize = 0;
                try
                {
                    var fileInfo = _sshService.GetFileInfo(item.Path);
                    fileSize = fileInfo?.Length ?? 0;
                }
                catch { }

                // 添加到下载管理器（非阻塞）
                DownloadManager.Instance.AddDownloadTask(item.Name, item.Path, saveDialog.FileName, fileSize, _sshService);
                UpdateStatusText("已添加到下载队列");
            }
        }

        private void BtnDownloadManager_Click(object sender, RoutedEventArgs e)
        {
            var downloadManagerWindow = new DownloadManagerWindow();
            downloadManagerWindow.Owner = this;
            downloadManagerWindow.ShowDialog();
        }

        private void BtnUploadManager_Click(object sender, RoutedEventArgs e)
        {
            var uploadManagerWindow = new UploadManagerWindow();
            uploadManagerWindow.Owner = this;
            uploadManagerWindow.ShowDialog();
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is not SftpFileItem item)
                return;

            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }

            var result = MessageBox.Show($"确定要删除 {item.Name} 吗?", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    UpdateStatusText("删除中...");
                    if (item.IsDirectory)
                    {
                        // SSH.NET没有直接的删除目录方法，需要递归删除
                        await DeleteDirectoryAsync(item.Path);
                    }
                    else
                    {
                        await _sshService.DeleteFileAsync(item.Path);
                    }
                    await LoadFilesAsync(_currentPath);
                    UpdateStatusText("删除成功");
                }
                catch (Exception ex)
                {
                    UpdateStatusText($"删除失败: {ex.Message}");
                }
            }
        }

        private async System.Threading.Tasks.Task DeleteDirectoryAsync(string path)
        {
            if (_sshService == null)
            {
                return;
            }
            
            var files = await _sshService.ListDirectoryAsync(path);
            foreach (var file in files)
            {
                if (file.Name == "." || file.Name == "..")
                    continue;

                var filePath = Path.Combine(path, file.Name).Replace('\\', '/');
                if (file.IsDirectory)
                {
                    await DeleteDirectoryAsync(filePath);
                }
                else
                {
                    await _sshService.DeleteFileAsync(filePath);
                }
            }
            await _sshService.DeleteDirectoryAsync(path);
        }

        private async void BtnRename_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is not SftpFileItem item)
                return;

            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }

            var newName = Microsoft.VisualBasic.Interaction.InputBox("请输入新名称:", "重命名", item.Name);
            if (!string.IsNullOrEmpty(newName) && newName != item.Name)
            {
                try
                {
                    UpdateStatusText("重命名中...");
                    var newPath = Path.Combine(_currentPath, newName).Replace('\\', '/');
                    await _sshService.RenameFileAsync(item.Path, newPath);
                    await LoadFilesAsync(_currentPath);
                    UpdateStatusText("重命名成功");
                }
                catch (Exception ex)
                {
                    UpdateStatusText($"重命名失败: {ex.Message}");
                }
            }
        }

        private async void BtnNewFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }
            
            var folderName = Microsoft.VisualBasic.Interaction.InputBox("请输入文件夹名称:", "新建文件夹", "新建文件夹");
            if (!string.IsNullOrEmpty(folderName))
            {
                try
                {
                    UpdateStatusText("创建中...");
                    var newPath = Path.Combine(_currentPath, folderName).Replace('\\', '/');
                    await _sshService.CreateDirectoryAsync(newPath);
                    await LoadFilesAsync(_currentPath);
                    UpdateStatusText("创建成功");
                }
                catch (Exception ex)
                {
                    UpdateStatusText($"创建失败: {ex.Message}");
                }
            }
        }

        private async void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListView.SelectedItem is not SftpFileItem item || item.IsDirectory)
                return;

            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }

            try
            {
                UpdateStatusText("准备编辑...");
                
                // 创建临时目录
                var tempDir = Path.Combine(Path.GetTempPath(), "SshClientTemp");
                Directory.CreateDirectory(tempDir);
                
                // 下载文件到临时目录
                var tempFile = Path.Combine(tempDir, item.Name);
                await _sshService.DownloadFileAsync(item.Path, tempFile);
                
                // 记录文件最后修改时间
                var originalLastWriteTime = File.GetLastWriteTime(tempFile);
                
                // 调用系统默认编辑器打开文件
                UpdateStatusText("编辑中...");
                var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });
                
                if (process != null)
                {
                    // 等待编辑器进程结束
                    process.WaitForExit();
                    
                    // 检查文件是否被修改
                    var newLastWriteTime = File.GetLastWriteTime(tempFile);
                    if (newLastWriteTime != originalLastWriteTime)
                    {
                        // 文件被修改，上传回服务器
                        UpdateStatusText("上传修改...");
                        await _sshService.UploadFileAsync(tempFile, item.Path);
                        await LoadFilesAsync(_currentPath);
                        UpdateStatusText($"文件 {item.Name} 已成功编辑并上传");
                        
                        // 记录编辑历史
                        string connectionId = string.Empty;
                        if (_sshService != null && _sshService.IsConnected)
                        {
                            // 获取连接标识
                            var connectionInfo = ConnectionManagerService.Instance.GetConnectionInfo(_sshService);
                            if (connectionInfo != null)
                            {
                                connectionId = $"{connectionInfo.Host}:{connectionInfo.Port}:{connectionInfo.Username}";
                            }
                        }
                        EditHistoryManager.AddEditHistory(item.Name, item.Path, connectionId);
                    }
                    else
                    {
                        UpdateStatusText("文件未修改");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText($"编辑失败: {ex.Message}");
            }
        }

        private void BtnEditHistory_Click(object sender, RoutedEventArgs e)
        {
            var editHistoryWindow = new EditHistoryWindow();
            editHistoryWindow.Owner = this;
            editHistoryWindow.ShowDialog();
        }



        private async void BtnNewFile_Click(object sender, RoutedEventArgs e)
        {
            if (_sshService == null)
            {
                UpdateStatusText("请选择一个连接");
                return;
            }
            
            var fileName = Microsoft.VisualBasic.Interaction.InputBox("请输入文件名:", "新建文件", "newfile.txt");
            if (!string.IsNullOrEmpty(fileName))
            {
                try
                {
                    // 检查同名文件是否存在
                    var remotePath = Path.Combine(_currentPath, fileName).Replace('\\', '/');
                    var files = await _sshService.ListDirectoryAsync(_currentPath);
                    if (files.Any(file => file.Name == fileName))
                    {
                        MessageBox.Show($"文件 {fileName} 已存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 创建临时文件
                    var tempDir = Path.Combine(Path.GetTempPath(), "SshClientTemp");
                    Directory.CreateDirectory(tempDir);
                    var tempFile = Path.Combine(tempDir, fileName);
                    File.Create(tempFile).Close();
                    
                    // 上传到服务器
                    await _sshService.UploadFileAsync(tempFile, remotePath);
                    await LoadFilesAsync(_currentPath);
                    UpdateStatusText($"文件 {fileName} 已成功创建");
                }
                catch (Exception ex)
                {
                    UpdateStatusText($"创建文件失败: {ex.Message}");
                }
            }
        }

        private async void PathTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (_sshService == null)
                {
                    UpdateStatusText("请选择一个连接");
                    return;
                }
                
                var path = PathTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    // 验证路径，防止路径遍历
                    if (!IsPathSafe(path))
                    {
                        UpdateStatusText("错误: 路径包含不安全的内容");
                        // 恢复原来的路径
                        PathTextBox.Text = _currentPath;
                        return;
                    }
                    
                    // 确保路径以/开头
                    if (!path.StartsWith("/"))
                    {
                        path = "/" + path;
                    }
                    // 确保路径格式正确
                    path = path.Replace('\\', '/');
                    // 移除末尾的/
                    if (path.Length > 1 && path.EndsWith("/"))
                    {
                        path = path.Substring(0, path.Length - 1);
                    }
                    
                    try
                    {
                        // 验证路径是否存在
                        await _sshService.ListDirectoryAsync(path);
                        _currentPath = path;
                        _pathHistory.Clear();
                        await LoadFilesAsync(path);
                    }
                    catch (Exception ex)
                    {
                        UpdateStatusText($"路径不存在: {ex.Message}");
                        // 恢复原来的路径
                        PathTextBox.Text = _currentPath;
                    }
                }
            }
        }

        private bool IsPathSafe(string path)
        {
            // 检查路径是否包含路径遍历字符
            var dangerousPatterns = new[]
            {
                "../", // 上一级目录
                "..\\", // 上一级目录（Windows格式）
                "/../", // 上一级目录
                "\\..\\", // 上一级目录（Windows格式）
                "/..", // 上一级目录（末尾）
                "\\.." // 上一级目录（末尾，Windows格式）
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (path.Contains(pattern))
                {
                    return false;
                }
            }

            return true;
        }

        #region 右键菜单事件处理

        private void ContextMenu_Upload_Click(object sender, RoutedEventArgs e)
        {
            BtnUpload_Click(sender, e);
        }

        private void ContextMenu_Download_Click(object sender, RoutedEventArgs e)
        {
            BtnDownload_Click(sender, e);
        }

        private void ContextMenu_Edit_Click(object sender, RoutedEventArgs e)
        {
            BtnEdit_Click(sender, e);
        }

        private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
        {
            BtnDelete_Click(sender, e);
        }

        private void ContextMenu_Rename_Click(object sender, RoutedEventArgs e)
        {
            BtnRename_Click(sender, e);
        }

        private void ContextMenu_NewFolder_Click(object sender, RoutedEventArgs e)
        {
            BtnNewFolder_Click(sender, e);
        }

        private void ContextMenu_NewFile_Click(object sender, RoutedEventArgs e)
        {
            BtnNewFile_Click(sender, e);
        }

        private void ContextMenu_Copy_Click(object sender, RoutedEventArgs e)
        {
            BtnCopy_Click(sender, e);
        }

        private void ContextMenu_Cut_Click(object sender, RoutedEventArgs e)
        {
            BtnCut_Click(sender, e);
        }

        private void ContextMenu_Paste_Click(object sender, RoutedEventArgs e)
        {
            BtnPaste_Click(sender, e);
        }

        private void ContextMenu_Refresh_Click(object sender, RoutedEventArgs e)
        {
            BtnRefresh_Click(sender, e);
        }

        private void ContextMenu_Back_Click(object sender, RoutedEventArgs e)
        {
            BtnBack_Click(sender, e);
        }

        #endregion
    }

    public class SftpFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string LastWriteTime { get; set; } = string.Empty;
        public string Permissions { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
    }
}