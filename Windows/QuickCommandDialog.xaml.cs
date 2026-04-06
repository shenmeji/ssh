using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SimpleSshClient.Models;

namespace SimpleSshClient.Windows
{
    public partial class QuickCommandDialog : Window
    {
        private List<QuickCommand> _quickCommands;
        private readonly string _quickCommandsPath;
        public string? Command { get; private set; }

        public QuickCommandDialog()
        {
            InitializeComponent();
            // 使用程序目录存储快速命令
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            _quickCommandsPath = Path.Combine(exeDir, "quickcommands.json");
            _quickCommands = LoadQuickCommands();
            RefreshList();
        }

        private List<QuickCommand> LoadQuickCommands()
        {
            var defaultCommands = new List<QuickCommand>
            {
                new QuickCommand { Name = "查看磁盘使用", Command = "df -h", SortOrder = 1 },
                new QuickCommand { Name = "查看内存使用", Command = "free -h", SortOrder = 2 },
                new QuickCommand { Name = "查看系统负载", Command = "uptime", SortOrder = 3 },
                new QuickCommand { Name = "查看进程", Command = "ps aux", SortOrder = 4 },
                new QuickCommand { Name = "查看网络连接", Command = "netstat -tuln", SortOrder = 5 },
                new QuickCommand { Name = "列出当前目录", Command = "ls -la", SortOrder = 6 },
                new QuickCommand { Name = "查看当前用户", Command = "whoami", SortOrder = 7 },
                new QuickCommand { Name = "查看系统信息", Command = "uname -a", SortOrder = 8 }
            };

            if (!File.Exists(_quickCommandsPath))
            {
                SaveQuickCommands(defaultCommands);
                return defaultCommands;
            }

            try
            {
                var json = File.ReadAllText(_quickCommandsPath);
                var commands = JsonSerializer.Deserialize<List<QuickCommand>>(json) ?? defaultCommands;
                return commands.OrderBy(c => c.SortOrder).ToList();
            }
            catch
            {
                return defaultCommands;
            }
        }

        private void SaveQuickCommands(List<QuickCommand> commands)
        {
            var json = JsonSerializer.Serialize(commands, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_quickCommandsPath, json);
        }

        private void RefreshList()
        {
            _quickCommands = _quickCommands.OrderBy(c => c.SortOrder).ToList();
            QuickCommandsListBox.ItemsSource = null;
            QuickCommandsListBox.ItemsSource = _quickCommands;
        }

        private void QuickCommandsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = QuickCommandsListBox.SelectedItem != null;
            EditButton.IsEnabled = hasSelection;
            DeleteButton.IsEnabled = hasSelection;

            if (QuickCommandsListBox.SelectedItem is QuickCommand cmd)
            {
                CommandTextBox.Text = cmd.Command;
            }
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new QuickCommandEditDialog();
            if (dialog.ShowDialog() == true && dialog.Command != null)
            {
                _quickCommands.Add(dialog.Command);
                SaveQuickCommands(_quickCommands);
                RefreshList();
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            EditSelectedCommand();
        }

        private void QuickCommandsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (QuickCommandsListBox.SelectedItem is QuickCommand cmd)
            {
                cmd.ExecuteCount++;
                cmd.LastExecutedAt = DateTime.Now;
                SaveQuickCommands(_quickCommands);
                Command = cmd.Command;
                DialogResult = true;
                Close();
            }
        }

        private void EditSelectedCommand()
        {
            if (QuickCommandsListBox.SelectedItem is QuickCommand cmd)
            {
                var dialog = new QuickCommandEditDialog(cmd);
                if (dialog.ShowDialog() == true && dialog.Command != null)
                {
                    cmd.Name = dialog.Command.Name;
                    cmd.Command = dialog.Command.Command;
                    cmd.SortOrder = dialog.Command.SortOrder;
                    SaveQuickCommands(_quickCommands);
                    RefreshList();
                    QuickCommandsListBox.SelectedItem = cmd;
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuickCommandsListBox.SelectedItem is QuickCommand cmd)
            {
                var result = MessageBox.Show($"确定要删除命令 \"{cmd.Name}\" 吗?", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _quickCommands.Remove(cmd);
                    SaveQuickCommands(_quickCommands);
                    RefreshList();
                }
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_quickCommands.Count == 0)
            {
                MessageBox.Show("没有可导出的命令", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Title = "导出快速命令",
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = "quickcommands.json"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_quickCommands, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(saveDialog.FileName, json);
                    MessageBox.Show($"成功导出 {_quickCommands.Count} 个快速命令", "导出成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Title = "导入快速命令",
                Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    var json = File.ReadAllText(openDialog.FileName);
                    var importedCommands = JsonSerializer.Deserialize<List<QuickCommand>>(json) ?? new List<QuickCommand>();
                    if (importedCommands.Count == 0)
                    {
                        MessageBox.Show("没有找到可导入的命令", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int importedCount = 0;
                    foreach (var cmd in importedCommands)
                    {
                        var existing = _quickCommands.FirstOrDefault(c => c.Name == cmd.Name && c.Command == cmd.Command);
                        if (existing == null)
                        {
                            _quickCommands.Add(cmd);
                            importedCount++;
                        }
                    }

                    if (importedCount > 0)
                    {
                        SaveQuickCommands(_quickCommands);
                        RefreshList();
                    }

                    string message = $"导入完成！\n\n成功导入: {importedCount} 个\n跳过(已存在): {importedCommands.Count - importedCount} 个";
                    MessageBox.Show(message, "导入完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommandTextBox.Text))
            {
                MessageBox.Show("请输入命令", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (QuickCommandsListBox.SelectedItem is QuickCommand cmd)
            {
                cmd.ExecuteCount++;
                cmd.LastExecutedAt = DateTime.Now;
                SaveQuickCommands(_quickCommands);
            }

            Command = CommandTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
