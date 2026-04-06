using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SimpleSshClient.Models;

namespace SimpleSshClient.Windows
{
    public partial class CommandHistoryDialog : Window
    {
        private readonly List<string> _commandHistory;
        public string? Command { get; private set; }

        public CommandHistoryDialog(List<string> commandHistory)
        {
            InitializeComponent();
            _commandHistory = commandHistory;
            CommandHistoryListBox.ItemsSource = _commandHistory;
        }

        private void CommandHistoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CommandHistoryListBox.SelectedItem is string command)
            {
                CommandTextBox.Text = command;
            }
        }

        private void CommandHistoryListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (CommandHistoryListBox.SelectedItem is string command)
            {
                CommandTextBox.Text = command;
            }
        }

        private void ExecuteButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommandTextBox.Text))
            {
                MessageBox.Show("请输入命令", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Command = CommandTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(CommandTextBox.Text))
            {
                MessageBox.Show("请输入命令", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 打开快速命令编辑对话框，添加新命令，并传递当前命令内容
            var quickCommand = new QuickCommand { Command = CommandTextBox.Text };
            var dialog = new QuickCommandEditDialog(quickCommand, "保存快速命令");
            if (dialog.ShowDialog() == true && dialog.Command != null)
            {
                // 保存到快速命令列表
                try
                {
                    // 使用与QuickCommandDialog相同的路径
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    var exeDir = System.IO.Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    var quickCommandsPath = System.IO.Path.Combine(exeDir, "quickcommands.json");
                    
                    // 加载现有命令
                    List<QuickCommand> quickCommands = new List<QuickCommand>();
                    if (System.IO.File.Exists(quickCommandsPath))
                    {
                        var json = System.IO.File.ReadAllText(quickCommandsPath);
                        quickCommands = System.Text.Json.JsonSerializer.Deserialize<List<QuickCommand>>(json) ?? new List<QuickCommand>();
                    }
                    
                    // 添加新命令
                    quickCommands.Add(dialog.Command);
                    
                    // 保存到文件
                    var jsonData = System.Text.Json.JsonSerializer.Serialize(quickCommands, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    System.IO.File.WriteAllText(quickCommandsPath, jsonData);
                    
                    MessageBox.Show("命令已成功保存到快速命令", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                
                DialogResult = false;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}