using System;

namespace SimpleSshClient.Models
{
    public class MessageItem
    {
        public string Time { get; set; }
        public string Message { get; set; }

        public MessageItem(string message)
        {
            Time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Message = message;
        }
    }
}