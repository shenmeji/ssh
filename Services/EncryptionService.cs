using System;
using System.Security.Cryptography;
using System.Text;

namespace SimpleSshClient.Services
{
    public static class EncryptionService
    {
        // DPAPI 熵值
        private static readonly byte[] DpapiEntropy = Encoding.UTF8.GetBytes("SimpleSshClient_Entropy_2024");

        // 使用DPAPI加密（本机存储）
        public static string EncryptWithDpapi(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedBytes = ProtectedData.Protect(
                    plainBytes,
                    DpapiEntropy,
                    DataProtectionScope.CurrentUser);

                return Convert.ToBase64String(encryptedBytes);
            }
            catch
            {
                return plainText;
            }
        }

        // 使用DPAPI解密（本机存储）
        public static string DecryptWithDpapi(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                byte[] decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes,
                    DpapiEntropy,
                    DataProtectionScope.CurrentUser);

                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch
            {
                return encryptedText;
            }
        }

        // 使用AES256加密（导出导入）
        public static string EncryptWithAes(string plainText, string password)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            using (var aes = Aes.Create())
            {
                // 生成随机IV
                aes.GenerateIV();
                byte[] iv = aes.IV;

                // 使用密码派生密钥
                using (var deriveBytes = new Rfc2898DeriveBytes(password, iv, 10000))
                {
                    aes.Key = deriveBytes.GetBytes(32); // 256位密钥
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var encryptor = aes.CreateEncryptor())
                    using (var msEncrypt = new System.IO.MemoryStream())
                    {
                        // 先写入IV
                        msEncrypt.Write(iv, 0, iv.Length);
                        
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new System.IO.StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        return Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }
        }

        // 使用AES256解密（导出导入）
        public static string DecryptWithAes(string encryptedText, string password)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
                using (var aes = Aes.Create())
                {
                    // 提取IV（前16字节）
                    byte[] iv = new byte[16];
                    Array.Copy(encryptedBytes, 0, iv, 0, iv.Length);

                    // 提取密文
                    byte[] cipherText = new byte[encryptedBytes.Length - iv.Length];
                    Array.Copy(encryptedBytes, iv.Length, cipherText, 0, cipherText.Length);

                    // 使用密码派生密钥
                    using (var deriveBytes = new Rfc2898DeriveBytes(password, iv, 10000))
                    {
                        aes.Key = deriveBytes.GetBytes(32); // 256位密钥
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using (var decryptor = aes.CreateDecryptor())
                        using (var msDecrypt = new System.IO.MemoryStream(cipherText))
                        using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                        using (var srDecrypt = new System.IO.StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        // 兼容旧版本的加密方法
        public static string Encrypt(string plainText)
        {
            return EncryptWithDpapi(plainText);
        }

        // 兼容旧版本的解密方法
        public static string Decrypt(string encryptedText)
        {
            return DecryptWithDpapi(encryptedText);
        }
    }
}
