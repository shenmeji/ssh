using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Input;
using Microsoft.Win32;
using SimpleSshClient.Services;
using SimpleSshClient.Models;

namespace SimpleSshClient
{
    public partial class TerminalControl : UserControl
    {
        private readonly List<string> _commandHistory = new();
        private int _historyIndex = -1;
        private static readonly Regex AnsiEscapeRegex = new Regex(@"\x1b\[[0-9;?]*[a-zA-Z]|\x1b\][0-9];[^07]*\x07", RegexOptions.Compiled);
        private SshService? _sshService;
        private bool _isResizing;
        private Brush _currentForeground = Brushes.White;
        private readonly LogService _logService = new LogService();
        
        // 虚拟滚动相关
        private const int MAX_LINES = 10000; // 最大行数限制
        private const int KEEP_LINES = 8000; // 清理后保留的行数
        private int _totalLines = 0;

        public event EventHandler<string>? CommandSent;
        public event EventHandler? InterruptRequested;

        public TerminalControl()
        {
            InitializeComponent();
            Loaded += TerminalControl_Loaded;
            SizeChanged += TerminalControl_SizeChanged;
        }

        public void SetSshService(SshService service)
        {
            _sshService = service;
            UpdateTerminalSize();
        }

        private Brush _defaultForeground = Brushes.White;
        
        // 缓存常用的颜色Brush对象
        private static readonly Dictionary<int, Brush> _colorCache = new Dictionary<int, Brush>
        {
            { 0, Brushes.White }, // 重置到默认颜色
            { 30, Brushes.Black },
            { 31, Brushes.Red },
            { 32, Brushes.Green },
            { 33, Brushes.Yellow },
            { 34, Brushes.Blue },
            { 35, Brushes.Magenta },
            { 36, Brushes.Cyan },
            { 37, Brushes.White },
            { 90, Brushes.Gray },
            { 91, Brushes.Red },
            { 92, Brushes.LightGreen },
            { 93, Brushes.LightYellow },
            { 94, Brushes.LightBlue },
            { 95, Brushes.LightPink },
            { 96, Brushes.LightCyan },
            { 97, Brushes.White }
        };

        private void TerminalControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateTerminalSize();
        }

        private void TerminalControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isResizing)
            {
                _isResizing = true;
                UpdateTerminalSize();
                _isResizing = false;
            }
        }

        private void UpdateTerminalSize()
        {
            if (_sshService != null && OutputTextBox.ActualWidth > 0 && OutputTextBox.ActualHeight > 0)
            {
                var fontSize = 14;
                var charWidth = fontSize * 0.6;
                var lineHeight = fontSize * 1.5;

                var columns = Math.Max(80, (uint)(OutputTextBox.ActualWidth / charWidth));
                var rows = Math.Max(24, (uint)(OutputTextBox.ActualHeight / lineHeight));

                _sshService.ResizeTerminal(columns, rows);
            }
        }

        public void AppendOutput(string text)
        {
            // 使用异步调用，避免阻塞UI线程
            Task.Run(() =>
            {
                // 预处理文本，减轻UI线程负担
                var processedText = text;
                
                // 异步更新UI
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var paragraph = OutputTextBox.Document.Blocks.LastBlock as Paragraph;
                        if (paragraph == null)
                        {
                            paragraph = new Paragraph();
                            OutputTextBox.Document.Blocks.Add(paragraph);
                        }
                        
                        // 处理ANSI转义序列和颜色
                        ProcessAnsiText(processedText, paragraph);
                        
                        // 更新行数计数
                        _totalLines += processedText.Split('\n').Length - 1;
                        
                        // 检查是否需要清理行数
                        if (_totalLines > MAX_LINES)
                        {
                            // 清理行数操作也可能耗时，使用Task.Run
                            Task.Run(() =>
                            {
                                Dispatcher.BeginInvoke(() =>
                                {
                                    CleanupLines();
                                });
                            });
                        }
                        
                        // 自动滚动到底部
                        OutputTextBox.ScrollToEnd();
                    }
                    catch (Exception ex)
                    {
                        _logService.Error("更新终端输出失败", ex);
                    }
                });
            });
        }
        
        private void CleanupLines()
        {
            try
            {
                // 计算需要保留的段落数
                int linesToKeep = KEEP_LINES;
                int currentLines = 0;
                var paragraphsToKeep = new List<Block>();
                
                // 从最后开始遍历，保留最近的内容
                foreach (var block in OutputTextBox.Document.Blocks)
                {
                    if (block is Paragraph paragraph)
                    {
                        int paragraphLines = paragraph.Inlines.Count;
                        if (currentLines + paragraphLines <= linesToKeep)
                        {
                            paragraphsToKeep.Add(block);
                            currentLines += paragraphLines;
                        }
                    }
                }
                
                // 清理文档并添加保留的内容
                OutputTextBox.Document.Blocks.Clear();
                foreach (var block in paragraphsToKeep)
                {
                    OutputTextBox.Document.Blocks.Add(block);
                }
                
                // 更新行数计数
                _totalLines = currentLines;
            }
            catch (Exception ex)
            {
                _logService.Error("清理终端行失败", ex);
            }
        }

        private void ProcessAnsiText(string text, Paragraph paragraph)
        {
            // 快速路径：如果没有ANSI转义序列，直接添加文本
            if (!AnsiEscapeRegex.IsMatch(text))
            {
                var run = new Run(text);
                run.Foreground = _currentForeground;
                paragraph.Inlines.Add(run);
                return;
            }
            
            int lastIndex = 0;
            var matches = AnsiEscapeRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                // 添加匹配前的文本
                if (match.Index > lastIndex)
                {
                    var run = new Run(text.Substring(lastIndex, match.Index - lastIndex));
                    run.Foreground = _currentForeground;
                    paragraph.Inlines.Add(run);
                }
                
                // 处理ANSI转义序列
                ProcessAnsiEscapeSequence(match.Value);
                
                lastIndex = match.Index + match.Length;
            }
            
            // 添加剩余的文本
            if (lastIndex < text.Length)
            {
                var run = new Run(text.Substring(lastIndex));
                run.Foreground = _currentForeground;
                paragraph.Inlines.Add(run);
            }
        }

        public void ApplyTheme(TerminalTheme theme)
        {
            try
            {
                _logService.Info($"应用主题: {theme.Name}");
                
                // 使用主题对象中的实际颜色值
                SolidColorBrush backgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Background));
                SolidColorBrush foregroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Foreground));
                SolidColorBrush selectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.Selection));
                
                // 输入框背景色与主体终端颜色一致
                SolidColorBrush inputBackgroundBrush = backgroundBrush;
                
                // 保存当前前景色和默认前景色
                _currentForeground = foregroundBrush;
                _defaultForeground = foregroundBrush;
                
                // 设置终端输出区域颜色
                OutputTextBox.Background = backgroundBrush;
                OutputTextBox.Foreground = foregroundBrush;
                OutputTextBox.SelectionBrush = selectionBrush;
                
                // 更新所有已存在的Run对象的前景色
                UpdateExistingTextForeground(foregroundBrush);
                
                // 设置输入框颜色（无边框）
                InputTextBox.Background = inputBackgroundBrush;
                InputTextBox.Foreground = foregroundBrush;
                InputTextBox.SelectionBrush = selectionBrush;
                InputTextBox.BorderThickness = new Thickness(0);
                InputTextBox.BorderBrush = null;
                
                // 更新父容器的背景色
                var grid = (Grid)InputTextBox.Parent;
                if (grid != null)
                {
                    grid.Background = inputBackgroundBrush;
                }
                
                _logService.Info($"主题 {theme.Name} 应用成功");
            }
            catch (Exception ex)
            {
                _logService.Error("主题应用失败", ex);
                
                // 颜色转换失败，使用默认值
                _currentForeground = Brushes.White;
                _defaultForeground = Brushes.White;
                OutputTextBox.Background = Brushes.Black;
                OutputTextBox.Foreground = Brushes.White;
                OutputTextBox.SelectionBrush = Brushes.Gray;
                
                // 更新所有已存在的Run对象的前景色
                UpdateExistingTextForeground(Brushes.White);
                
                InputTextBox.Background = Brushes.DarkGray;
                InputTextBox.Foreground = Brushes.White;
                InputTextBox.SelectionBrush = Brushes.Gray;
                InputTextBox.BorderThickness = new Thickness(0);
                InputTextBox.BorderBrush = null;
                
                var grid = (Grid)InputTextBox.Parent;
                if (grid != null)
                {
                    grid.Background = Brushes.DarkGray;
                }
                
                _logService.Info("已应用默认主题");
            }
        }

        private void UpdateExistingTextForeground(Brush foregroundBrush)
        {
            // 遍历所有段落和Run对象，更新前景色
            foreach (var block in OutputTextBox.Document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is Run run)
                        {
                            run.Foreground = foregroundBrush;
                        }
                    }
                }
            }
        }

        private void ProcessAnsiEscapeSequence(string sequence)
        {
            try
            {
                // 移除ESC字符和结束字符
                if (sequence.Length < 3)
                    return;
                
                var code = sequence.Substring(2, sequence.Length - 3);
                var codes = code.Split(';');
                
                foreach (var c in codes)
                {
                    if (int.TryParse(c, out int codeValue))
                    {
                        // 使用缓存的颜色Brush
                        if (_colorCache.TryGetValue(codeValue, out Brush color))
                        {
                            if (codeValue == 0)
                                _currentForeground = _defaultForeground;
                            else
                                _currentForeground = color;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error("处理ANSI转义序列失败", ex);
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendInput();
        }

        private void InterruptButton_Click(object sender, RoutedEventArgs e)
        {
            InterruptRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Document.Blocks.Clear();
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (Clipboard.ContainsText())
            {
                InputTextBox.Text = Clipboard.GetText();
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 复制终端中选中的文本
                if (OutputTextBox.Selection.Text != string.Empty)
                {
                    Clipboard.SetText(OutputTextBox.Selection.Text);
                }
                else
                {
                    // 如果没有选中的文本，复制整个终端内容
                    var textRange = new TextRange(OutputTextBox.Document.ContentStart, OutputTextBox.Document.ContentEnd);
                    Clipboard.SetText(textRange.Text);
                }
            }
            catch (Exception ex)
            {
                _logService.Error("复制失败", ex);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "保存终端输出",
                Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
                FileName = $"terminal_output_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var textRange = new TextRange(OutputTextBox.Document.ContentStart, OutputTextBox.Document.ContentEnd);
                    File.WriteAllText(saveDialog.FileName, textRange.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Windows.CommandHistoryDialog(_commandHistory);
            // 设置Owner为主窗口
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                dialog.Owner = mainWindow;
            }
            else
            {
                // 如果主窗口为null，尝试获取当前控件所在的窗口
                var currentWindow = Window.GetWindow(this);
                if (currentWindow != null)
                {
                    dialog.Owner = currentWindow;
                }
            }
            // 确保窗口位置正确
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.Command))
            {
                // 直接使用SshService发送命令
                if (_sshService != null)
                {
                    _sshService.SendCommand(dialog.Command);
                    // 将命令添加到历史记录
                    _commandHistory.Add(dialog.Command);
                    _historyIndex = _commandHistory.Count;
                }
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendInput();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                NavigateHistory(-1);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                NavigateHistory(1);
                e.Handled = true;
            }
        }

        private void SendInput()
        {
            var command = InputTextBox.Text.Trim();
            if (!string.IsNullOrEmpty(command))
            {
                // 验证命令，防止命令注入
                if (IsCommandSafe(command))
                {
                    _commandHistory.Add(command);
                    _historyIndex = _commandHistory.Count;
                    CommandSent?.Invoke(this, command);
                    InputTextBox.Clear();
                }
                else
                {
                    AppendOutput("错误: 命令包含不安全的内容\n");
                }
            }
        }

        private bool IsCommandSafe(string command)
        {
            // 检查命令是否包含危险字符或命令
            var dangerousPatterns = new[]
            {
                ";", // 命令分隔符
                "|", // 管道符
                "&&", // 逻辑与
                "||", // 逻辑或
                "`", // 反引号
                "$(" // 命令替换
            };

            foreach (var pattern in dangerousPatterns)
            {
                if (command.Contains(pattern))
                {
                    return false;
                }
            }

            return true;
        }

        private void NavigateHistory(int direction)
        {
            if (_commandHistory.Count == 0)
                return;

            _historyIndex += direction;

            if (_historyIndex < 0)
                _historyIndex = 0;
            else if (_historyIndex >= _commandHistory.Count)
                _historyIndex = _commandHistory.Count - 1;

            if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
            {
                InputTextBox.Text = _commandHistory[_historyIndex];
                InputTextBox.CaretIndex = InputTextBox.Text.Length;
            }
        }
    }
}
