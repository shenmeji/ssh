using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SimpleSshClient.Models;

namespace SimpleSshClient.Services
{
    /// <summary>
    /// 连接存储服务，负责连接信息的保存、加载、导出和导入
    /// </summary>
    public class ConnectionStorageService
    {
        private readonly string _storagePath;
        private const long MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

        /// <summary>
        /// 初始化连接存储服务
        /// </summary>
        public ConnectionStorageService()
        {
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDir = Path.GetDirectoryName(exePath);
            _storagePath = Path.Combine(appDir ?? AppDomain.CurrentDomain.BaseDirectory, "connections.json");
        }

        /// <summary>
        /// 加载连接信息
        /// </summary>
        /// <returns>连接信息列表</returns>
        public List<ConnectionInfo> LoadConnections()
        {
            if (!File.Exists(_storagePath))
                return new List<ConnectionInfo>();

            try
            {
                var json = File.ReadAllText(_storagePath);
                var connections = JsonSerializer.Deserialize<List<ConnectionInfo>>(json) ?? new List<ConnectionInfo>();
                
                foreach (var conn in connections)
                {
                    if (!string.IsNullOrEmpty(conn.EncryptedPassword))
                    {
                        conn.Password = EncryptionService.Decrypt(conn.EncryptedPassword);
                    }
                }
                
                return connections;
            }
            catch
            {
                return new List<ConnectionInfo>();
            }
        }

        /// <summary>
        /// 保存连接信息
        /// </summary>
        /// <param name="connections">连接信息列表</param>
        public void SaveConnections(List<ConnectionInfo> connections)
        {
            foreach (var conn in connections)
            {
                if (!string.IsNullOrEmpty(conn.Password))
                {
                    conn.EncryptedPassword = EncryptionService.Encrypt(conn.Password);
                }
                else
                {
                    conn.EncryptedPassword = null;
                }
            }
            
            var json = JsonSerializer.Serialize(connections, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }

        /// <summary>
        /// 导出连接（支持不同的导出选项）
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <param name="connections">连接信息列表</param>
        /// <param name="useEncryption">是否使用加密</param>
        /// <param name="password">加密密码</param>
        public void ExportConnections(string filePath, List<ConnectionInfo> connections, bool useEncryption = false, string password = "")
        {
            // 创建导出数据结构
            var exportData = new ExportData
            {
                Metadata = new ExportMetadata
                {
                    AppMarker = "shenmeji_ssh",
                    EncryptionFlag = useEncryption ? 1 : 0,
                    Version = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                },
                Connections = new List<ConnectionInfo>()
            };
            
            // 处理验证密码
            if (useEncryption && !string.IsNullOrEmpty(password))
            {
                // 加密验证密码
                exportData.Metadata.VerificationPassword = EncryptionService.EncryptWithAes("shenmeji_ssh", password);
                exportData.Metadata.EncryptionFlag = 1;
            }
            else
            {
                // 明文导出时，验证密码为明文
                exportData.Metadata.VerificationPassword = "shenmeji_ssh";
                exportData.Metadata.EncryptionFlag = 0;
            }
            
            // 处理连接密码
            foreach (var originalConn in connections)
            {
                // 创建连接副本，避免修改原始连接
                var conn = new ConnectionInfo
                {
                    Id = originalConn.Id,
                    Name = originalConn.Name,
                    Host = originalConn.Host,
                    Port = originalConn.Port,
                    Username = originalConn.Username,
                    PrivateKeyPath = originalConn.PrivateKeyPath
                };
                
                // 获取原始密码（从Password字段，因为本地存储时使用的是这个字段）
                string originalPassword = originalConn.Password;
                
                // 解密原始密码（因为原始密码在本地存储时是使用DPAPI加密的）
                string decryptedPassword = string.Empty;
                if (!string.IsNullOrEmpty(originalPassword))
                {
                    decryptedPassword = EncryptionService.DecryptWithDpapi(originalPassword);
                }
                
                // 根据导出方式处理密码
                if (useEncryption && !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(decryptedPassword))
                {
                    // 加密导出：使用用户输入的密码进行AES256加密
                    conn.EncryptedPassword = EncryptionService.EncryptWithAes(decryptedPassword, password);
                }
                else
                {
                    // 明文导出：直接存储明文密码
                    conn.EncryptedPassword = decryptedPassword;
                }
                
                exportData.Connections.Add(conn);
            }
            
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 导入连接（支持不同的导入选项）
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <param name="password">加密密码</param>
        /// <returns>导入的连接信息列表</returns>
        /// <exception cref="FileNotFoundException">文件不存在</exception>
        /// <exception cref="InvalidOperationException">文件格式错误或密码错误</exception>
        public List<ConnectionInfo> ImportConnections(string filePath, string password = "")
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("导入文件不存在", filePath);
            }

            // 检查文件大小
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > MAX_FILE_SIZE)
            {
                throw new InvalidOperationException("导入文件过大，最大支持10MB");
            }

            // 读取文件内容
            var json = File.ReadAllText(filePath);
            
            // 反序列化文件内容
            var exportData = JsonSerializer.Deserialize<ExportData>(json);
            
            // 验证文件格式
            ValidateFileFormat(exportData);
            
            // 验证密码
            ValidatePassword(exportData, password);
            
            // 处理连接
            return ProcessConnections(exportData, password);
        }

        /// <summary>
        /// 验证文件格式
        /// </summary>
        /// <param name="exportData">导出数据</param>
        /// <exception cref="InvalidOperationException">文件格式错误</exception>
        private void ValidateFileFormat(ExportData exportData)
        {
            // 验证文件基本结构
            if (exportData == null)
            {
                throw new InvalidOperationException("文件格式错误：无法解析文件内容");
            }

            // 验证应用标记（简化版：直接检查AppMarker是否为shenmeji_ssh）
            if (exportData.Metadata == null || string.IsNullOrEmpty(exportData.Metadata.AppMarker) || exportData.Metadata.AppMarker != "shenmeji_ssh")
            {
                throw new InvalidOperationException("不是有效的shenmeji SSH导出文件");
            }

            // 验证连接列表
            if (exportData.Connections == null)
            {
                throw new InvalidOperationException("文件格式错误：缺少连接信息");
            }

            // 验证版本
            if (string.IsNullOrEmpty(exportData.Metadata.Version))
            {
                // 兼容旧版本，默认版本为1.0
                exportData.Metadata.Version = "1.0";
            }
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        /// <param name="exportData">导出数据</param>
        /// <param name="password">输入的密码</param>
        /// <exception cref="InvalidOperationException">密码错误或需要密码</exception>
        private void ValidatePassword(ExportData exportData, string password)
        {
            if (exportData.Metadata.EncryptionFlag == 1)
            {
                // 加密导出的文件，需要密码
                if (string.IsNullOrEmpty(password))
                {
                    throw new InvalidOperationException("需要密码");
                }
                
                // 验证密码是否正确
                if (!string.IsNullOrEmpty(exportData.Metadata.VerificationPassword))
                {
                    try
                    {
                        var decryptedVerification = EncryptionService.DecryptWithAes(exportData.Metadata.VerificationPassword, password);
                        if (decryptedVerification != "shenmeji_ssh")
                        {
                            throw new InvalidOperationException("密码错误");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("密码错误", ex);
                    }
                }
                else
                {
                    throw new InvalidOperationException("文件格式错误：缺少验证密码");
                }
            }
            else
            {
                // 明文导出时，验证VerificationPassword是否为明文的"shenmeji_ssh"
                if (!string.IsNullOrEmpty(exportData.Metadata.VerificationPassword) && exportData.Metadata.VerificationPassword != "shenmeji_ssh")
                {
                    throw new InvalidOperationException("文件格式错误：验证密码不正确");
                }
            }
        }

        /// <summary>
        /// 处理连接信息
        /// </summary>
        /// <param name="exportData">导出数据</param>
        /// <param name="password">输入的密码</param>
        /// <returns>处理后的连接信息列表</returns>
        /// <exception cref="InvalidOperationException">处理连接时出错</exception>
        private List<ConnectionInfo> ProcessConnections(ExportData exportData, string password)
        {
            var importedConnections = new List<ConnectionInfo>();
            
            // 处理连接密码
            foreach (var originalConn in exportData.Connections)
            {
                // 验证连接的必要字段
                ValidateConnection(originalConn);
                
                // 创建连接副本
                var conn = new ConnectionInfo
                {
                    Id = originalConn.Id,
                    Name = originalConn.Name,
                    Host = originalConn.Host,
                    Port = originalConn.Port,
                    Username = originalConn.Username,
                    PrivateKeyPath = originalConn.PrivateKeyPath
                };
                
                // 解密连接密码（如果需要）
                string decryptedPassword = string.Empty;
                if (!string.IsNullOrEmpty(originalConn.EncryptedPassword))
                {
                    if (exportData.Metadata.EncryptionFlag == 1)
                    {
                        // 加密导出的文件，需要解密
                        try
                        {
                            decryptedPassword = EncryptionService.DecryptWithAes(originalConn.EncryptedPassword, password);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("密码错误", ex);
                        }
                    }
                    else
                    {
                        // 明文导出的文件，直接使用
                        decryptedPassword = originalConn.EncryptedPassword;
                    }
                }
                
                // 存储解密后的明文密码，让SaveConnections方法来处理加密
                if (!string.IsNullOrEmpty(decryptedPassword))
                {
                    conn.Password = decryptedPassword;
                }
                
                importedConnections.Add(conn);
            }
            
            return importedConnections;
        }

        /// <summary>
        /// 验证连接信息
        /// </summary>
        /// <param name="connection">连接信息</param>
        /// <exception cref="InvalidOperationException">连接信息无效</exception>
        private void ValidateConnection(ConnectionInfo connection)
        {
            if (connection == null)
            {
                throw new InvalidOperationException("文件格式错误：连接信息为空");
            }

            // 验证必要字段
            if (string.IsNullOrEmpty(connection.Host))
            {
                throw new InvalidOperationException("文件格式错误：连接缺少主机信息");
            }

            if (string.IsNullOrEmpty(connection.Username))
            {
                throw new InvalidOperationException("文件格式错误：连接缺少用户名信息");
            }

            // 验证端口范围
            if (connection.Port < 1 || connection.Port > 65535)
            {
                throw new InvalidOperationException("文件格式错误：端口号无效");
            }
        }

        // 兼容旧版本的导出方法
        public void ExportConnections(string filePath, List<ConnectionInfo> connections)
        {
            ExportConnections(filePath, connections, false, "");
        }
    }
}
