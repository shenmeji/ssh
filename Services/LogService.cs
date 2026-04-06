using System;
using System.IO;
using System.Text;

namespace SimpleSshClient.Services
{
    public class LogService
    {
        private readonly string _logDirectory;
        private readonly string _logFile;

        public LogService()
        {
            // 获取程序根目录
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var exeDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            
            _logDirectory = Path.Combine(exeDir, "logs");
            Directory.CreateDirectory(_logDirectory);
            _logFile = Path.Combine(_logDirectory, $"log-{DateTime.Now:yyyyMMdd}.txt");
        }

        public void Info(string message)
        {
            WriteLog("INFO", message);
        }

        public void Warning(string message)
        {
            WriteLog("WARNING", message);
        }

        public void Error(string message, Exception? exception = null)
        {
            var logMessage = message;
            if (exception != null)
            {
                logMessage += $"\n异常信息: {exception.Message}\n堆栈跟踪: {exception.StackTrace}";
            }
            WriteLog("ERROR", logMessage);
        }

        private void WriteLog(string level, string message)
        {
            try
            {
                var sanitizedMessage = SanitizeSensitiveInfo(message);
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {sanitizedMessage}";
                using var writer = new StreamWriter(_logFile, true, Encoding.UTF8);
                writer.WriteLine(logEntry);
            }
            catch
            {
                // 日志写入失败时忽略，避免影响主程序
            }
        }

        private string SanitizeSensitiveInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // 屏蔽主机名和端口号
            message = System.Text.RegularExpressions.Regex.Replace(message, @"([0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}):([0-9]+)", "[HOST]:[PORT]");
            message = System.Text.RegularExpressions.Regex.Replace(message, @"([a-zA-Z0-9.-]+):([0-9]+)", "[HOST]:[PORT]");

            // 屏蔽用户名
            message = System.Text.RegularExpressions.Regex.Replace(message, @"用户名: ([^\n]+)", "用户名: [USERNAME]");

            // 屏蔽密码
            message = System.Text.RegularExpressions.Regex.Replace(message, @"密码: ([^\n]+)", "密码: [PASSWORD]");

            // 屏蔽私钥路径
            message = System.Text.RegularExpressions.Regex.Replace(message, @"PrivateKeyPath: ([^\n]+)", "PrivateKeyPath: [PATH]");

            // 屏蔽连接名称中的敏感信息
            message = System.Text.RegularExpressions.Regex.Replace(message, @"连接: ([^\n]+)", "连接: [CONNECTION]");

            return message;
        }
    }
}