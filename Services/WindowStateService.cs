using System;
using System.IO;
using System.Text.Json;

namespace SimpleSshClient.Services
{
    public class WindowPosition
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public bool IsMaximized { get; set; }
    }

    public class WindowStateService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SimpleSshClient",
            "windowState.json");

        public static WindowPosition? Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<WindowPosition>(json);
                }
            }
            catch
            {
            }
            return null;
        }

        public static void Save(WindowPosition state)
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
            }
        }
    }
}
