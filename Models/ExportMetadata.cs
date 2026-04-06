using System;

namespace SimpleSshClient.Models
{
    public class ExportMetadata
    {
        // 应用标记，用于验证文件来源
        public string? AppMarker { get; set; }
        
        // 加密标志，0表示明文，1表示加密
        public int EncryptionFlag { get; set; }
        
        // 验证密码，用于验证导入时的密码是否正确
        public string? VerificationPassword { get; set; }
        
        // 文件版本，用于兼容性处理
        public string? Version { get; set; }
    }
}