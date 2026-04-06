using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace SimpleSshClient.Windows
{
    public partial class EditHistoryWindow : Window
    {
        private readonly string _historyFilePath;
        private List<EditHistoryItem>? _historyItems;

        public EditHistoryWindow()
        {
            InitializeComponent();
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath) ?? System.AppDomain.CurrentDomain.BaseDirectory;
            _historyFilePath = Path.Combine(exeDir, "edithistory.json");
            LoadHistory();
            HistoryListView.ItemsSource = _historyItems;
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    _historyItems = JsonSerializer.Deserialize<List<EditHistoryItem>>(json) ?? new List<EditHistoryItem>();
                }
                else
                {
                    _historyItems = new List<EditHistoryItem>();
                }
            }
            catch
            {
                _historyItems = new List<EditHistoryItem>();
            }
        }

        private void SaveHistory()
        {
            try
            {
                var json = JsonSerializer.Serialize(_historyItems ?? new List<EditHistoryItem>(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch
            {
                // 保存失败，忽略
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            _historyItems?.Clear();
            SaveHistory();
            HistoryListView.ItemsSource = null;
            HistoryListView.ItemsSource = _historyItems;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class EditHistoryItem
    {
        public string FileName { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string EditTime { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
    }

    public static class EditHistoryManager
    {
        private static readonly string _historyFilePath;

        static EditHistoryManager()
        {
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath) ?? System.AppDomain.CurrentDomain.BaseDirectory;
            _historyFilePath = Path.Combine(exeDir, "edithistory.json");
        }

        public static void AddEditHistory(string fileName, string path, string connectionId)
        {
            try
            {
                List<EditHistoryItem> historyItems;
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    historyItems = JsonSerializer.Deserialize<List<EditHistoryItem>>(json) ?? new List<EditHistoryItem>();
                }
                else
                {
                    historyItems = new List<EditHistoryItem>();
                }

                var newItem = new EditHistoryItem
                {
                    FileName = fileName,
                    Path = path,
                    EditTime = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    ConnectionId = connectionId
                };

                historyItems.Add(newItem);

                var jsonResult = JsonSerializer.Serialize(historyItems, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, jsonResult);
            }
            catch
            {
                // 添加失败，忽略
            }
        }
    }
}