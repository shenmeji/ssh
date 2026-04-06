using System.Text.Json.Serialization;

namespace SimpleSshClient.Models
{
    public class TerminalTheme
    {
        public string Name { get; set; } = string.Empty;
        public string Foreground { get; set; } = "#FFFFFF";
        public string Background { get; set; } = "#000000";
        public string Cursor { get; set; } = "#FFFFFF";
        public string Selection { get; set; } = "#404040";
        public string ANSIBlack { get; set; } = "#000000";
        public string ANSIRed { get; set; } = "#CD0000";
        public string ANSIGreen { get; set; } = "#00CD00";
        public string ANSIYellow { get; set; } = "#CDCD00";
        public string ANSIBlue { get; set; } = "#0000CD";
        public string ANSIMagenta { get; set; } = "#CD00CD";
        public string ANSICyan { get; set; } = "#00CDCD";
        public string ANSIWhite { get; set; } = "#E5E5E5";
        public string ANSIBrightBlack { get; set; } = "#7F7F7F";
        public string ANSIBrightRed { get; set; } = "#FF0000";
        public string ANSIBrightGreen { get; set; } = "#00FF00";
        public string ANSIBrightYellow { get; set; } = "#FFFF00";
        public string ANSIBrightBlue { get; set; } = "#0000FF";
        public string ANSIBrightMagenta { get; set; } = "#FF00FF";
        public string ANSIBrightCyan { get; set; } = "#00FFFF";
        public string ANSIBrightWhite { get; set; } = "#FFFFFF";
    }

    public static class TerminalThemes
    {
        public static List<TerminalTheme> GetPresetThemes()
        {
            return new List<TerminalTheme>
            {
                new TerminalTheme
                {
                    Name = "默认",
                    Foreground = "#FFFFFF",
                    Background = "#000000",
                    Cursor = "#FFFFFF",
                    Selection = "#404040"
                },
                new TerminalTheme
                {
                    Name = "暗色",
                    Foreground = "#E0E0E0",
                    Background = "#1E1E1E",
                    Cursor = "#E0E0E0",
                    Selection = "#404040"
                },
                new TerminalTheme
                {
                    Name = "Solarized Dark",
                    Foreground = "#839496",
                    Background = "#002B36",
                    Cursor = "#839496",
                    Selection = "#073642",
                    ANSIBlack = "#002B36",
                    ANSIRed = "#DC322F",
                    ANSIGreen = "#859900",
                    ANSIYellow = "#B58900",
                    ANSIBlue = "#268BD2",
                    ANSIMagenta = "#D33682",
                    ANSICyan = "#2AA198",
                    ANSIWhite = "#EEE8D5",
                    ANSIBrightBlack = "#073642",
                    ANSIBrightRed = "#CB4B16",
                    ANSIBrightGreen = "#586E75",
                    ANSIBrightYellow = "#657B83",
                    ANSIBrightBlue = "#839496",
                    ANSIBrightMagenta = "#6C71C4",
                    ANSIBrightCyan = "#93A1A1",
                    ANSIBrightWhite = "#FDF6E3"
                },
                new TerminalTheme
                {
                    Name = "Solarized Light",
                    Foreground = "#657B83",
                    Background = "#FDF6E3",
                    Cursor = "#657B83",
                    Selection = "#EEE8D5",
                    ANSIBlack = "#002B36",
                    ANSIRed = "#DC322F",
                    ANSIGreen = "#859900",
                    ANSIYellow = "#B58900",
                    ANSIBlue = "#268BD2",
                    ANSIMagenta = "#D33682",
                    ANSICyan = "#2AA198",
                    ANSIWhite = "#EEE8D5",
                    ANSIBrightBlack = "#073642",
                    ANSIBrightRed = "#CB4B16",
                    ANSIBrightGreen = "#586E75",
                    ANSIBrightYellow = "#657B83",
                    ANSIBrightBlue = "#839496",
                    ANSIBrightMagenta = "#6C71C4",
                    ANSIBrightCyan = "#93A1A1",
                    ANSIBrightWhite = "#FDF6E3"
                },
                new TerminalTheme
                {
                    Name = "Monokai",
                    Foreground = "#F8F8F2",
                    Background = "#272822",
                    Cursor = "#F8F8F2",
                    Selection = "#49483E",
                    ANSIBlack = "#272822",
                    ANSIRed = "#F92672",
                    ANSIGreen = "#A6E22E",
                    ANSIYellow = "#F4BF75",
                    ANSIBlue = "#66D9EF",
                    ANSIMagenta = "#AE81FF",
                    ANSICyan = "#A1EFE4",
                    ANSIWhite = "#F8F8F2",
                    ANSIBrightBlack = "#75715E",
                    ANSIBrightRed = "#F92672",
                    ANSIBrightGreen = "#A6E22E",
                    ANSIBrightYellow = "#F4BF75",
                    ANSIBrightBlue = "#66D9EF",
                    ANSIBrightMagenta = "#AE81FF",
                    ANSIBrightCyan = "#A1EFE4",
                    ANSIBrightWhite = "#F8F8F2"
                }
            };
        }
    }
}