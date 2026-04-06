using System.Windows;
using SimpleSshClient.Models;

namespace SimpleSshClient.Windows
{
    public partial class QuickCommandEditDialog : Window
    {
        public QuickCommand? Command { get; set; }

        public QuickCommandEditDialog()
        {
            InitializeComponent();
            Title = "添加快速命令";
        }

        public QuickCommandEditDialog(QuickCommand command) : this()
        {
            Title = "编辑快速命令";
            NameTextBox.Text = command.Name;
            CommandTextBox.Text = command.Command;
            SortOrderTextBox.Text = command.SortOrder.ToString();
        }

        public QuickCommandEditDialog(QuickCommand command, string title) : this()
        {
            Title = title;
            NameTextBox.Text = command.Name;
            CommandTextBox.Text = command.Command;
            SortOrderTextBox.Text = command.SortOrder.ToString();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("请输入命令名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(CommandTextBox.Text))
            {
                MessageBox.Show("请输入命令内容", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(SortOrderTextBox.Text, out int sortOrder))
            {
                MessageBox.Show("排序值必须是数字", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Command = new QuickCommand
            {
                Name = NameTextBox.Text.Trim(),
                Command = CommandTextBox.Text.Trim(),
                SortOrder = sortOrder
            };

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
