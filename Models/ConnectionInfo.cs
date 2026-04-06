using System;
using System.Text.Json.Serialization;

namespace SimpleSshClient.Models
{
    public class ConnectionInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        [JsonIgnore]
        public string Password { get; set; } = string.Empty;
        public string? EncryptedPassword { get; set; }
        public string? PrivateKeyPath { get; set; }
        public string? Hostname { get; set; } // 主机名
    }
}
