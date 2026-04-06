namespace SimpleSshClient.Models
{
    public class ExportedConnectionInfo
    {
        // 文件标记，用于验证是否为本软件导出
        public string AppMarker { get; set; } = "shenmeji_ssh";
        
        // 加密类型：None（明文）、AES256（密码加密）
        public string EncryptionType { get; set; } = "None";
        
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 22;
        public string Username { get; set; } = string.Empty;
        public string? Password { get; set; } // 明文密码
        public string? EncryptedPassword { get; set; } // AES256加密密码
        public string? PrivateKeyPath { get; set; }
    }
}
