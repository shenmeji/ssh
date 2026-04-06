using System;
using System.IO;
using System.Text.Json;
using SimpleSshClient.Models;

namespace SimpleSshClient.Services
{
    public class ThemeService
    {
        private readonly string _themeConfigPath;

        public event EventHandler<TerminalTheme>? ThemeChanged;

        public ThemeService()
        {
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath) ?? System.AppDomain.CurrentDomain.BaseDirectory;
            _themeConfigPath = Path.Combine(exeDir, "theme.json");
        }

        public TerminalTheme GetCurrentTheme()
        {
            try
            {
                if (File.Exists(_themeConfigPath))
                {
                    var json = File.ReadAllText(_themeConfigPath);
                    return JsonSerializer.Deserialize<TerminalTheme>(json) ?? GetDefaultTheme();
                }
            }
            catch
            {
                // 读取失败，返回默认主题
            }
            return GetDefaultTheme();
        }

        public void SaveTheme(TerminalTheme theme)
        {
            try
            {
                var json = JsonSerializer.Serialize(theme, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_themeConfigPath, json);
                
                // 触发主题更改事件
                ThemeChanged?.Invoke(this, theme);
            }
            catch
            {
                // 保存失败，忽略
            }
        }

        public TerminalTheme GetDefaultTheme()
        {
            return new TerminalTheme
            {
                Name = "默认",
                Foreground = "#FFFFFF",
                Background = "#1a1a2e",
                Cursor = "#FFFFFF",
                Selection = "#40E0D0"
            };
        }
    }
}