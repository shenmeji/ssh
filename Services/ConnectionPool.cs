using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Renci.SshNet;
using SimpleSshClient.Models;
using SimpleSshClient.Windows;

namespace SimpleSshClient.Services
{
    public class ConnectionPool
    {
        private static ConnectionPool? _instance;
        private readonly Dictionary<string, ConnectionItem> _connections = new();
        private readonly object _lockObject = new();
        private readonly System.Timers.Timer _cleanupTimer;
        private readonly System.Timers.Timer _resourceMonitorTimer;
        private const int ConnectionTimeoutMinutes = 30;
        private int _maxConnections = 20; // 默认最大连接数设为20
        private int _minConnections = 5;  // 最小连接数
        private int _targetMaxConnections = 20; // 目标最大连接数
        private readonly string _configPath;

        public static ConnectionPool Instance => _instance ??= new ConnectionPool();

        private ConnectionPool()
        {
            // 初始化配置路径
            var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var appDir = Path.GetDirectoryName(exePath);
            _configPath = Path.Combine(appDir ?? AppDomain.CurrentDomain.BaseDirectory, "connection_pool_config.json");
            
            // 加载配置
            LoadConfig();
            
            // 初始化清理定时器，每5分钟检查一次
            _cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _cleanupTimer.Elapsed += CleanupIdleConnections;
            _cleanupTimer.Start();
            
            // 初始化资源监控定时器，每30秒检查一次
            _resourceMonitorTimer = new System.Timers.Timer(TimeSpan.FromSeconds(30).TotalMilliseconds);
            _resourceMonitorTimer.Elapsed += MonitorSystemResources;
            _resourceMonitorTimer.Start();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<ConnectionPoolConfig>(json);
                    if (config != null && config.MaxConnections > 0)
                    {
                        _maxConnections = config.MaxConnections;
                    }
                }
            }
            catch { }
        }

        public void SaveConfig()
        {
            try
            {
                var config = new ConnectionPoolConfig { MaxConnections = _maxConnections };
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
            }
            catch { }
        }

        public int MaxConnections
        {
            get => _maxConnections;
            set
            {
                if (value > 0)
                {
                    _maxConnections = value;
                    SaveConfig();
                }
            }
        }

        private class ConnectionPoolConfig
        {
            public int MaxConnections { get; set; }
        }

        public SshClient GetConnection(SimpleSshClient.Models.ConnectionInfo connectionInfo)
        {
            string key = $"{connectionInfo.Host}:{connectionInfo.Port}:{connectionInfo.Username}";
            
            lock (_lockObject)
            {
                if (_connections.TryGetValue(key, out var item))
                {
                    if (item.Client != null && item.Client.IsConnected)
                    {
                        item.UsageCount++;
                        item.LastUsed = DateTime.Now;
                        return item.Client;
                    }
                    else
                    {
                        _connections.Remove(key);
                    }
                }

                // 检查连接数是否达到上限
                if (_connections.Count >= _maxConnections)
                {
                    throw new InvalidOperationException("连接池达到最大连接数限制，请关闭一些连接后再尝试");
                }

                // 创建新连接
                SshClient client;
                if (!string.IsNullOrEmpty(connectionInfo.PrivateKeyPath))
                {
                    var privateKeyFile = new PrivateKeyFile(connectionInfo.PrivateKeyPath);
                    client = new SshClient(connectionInfo.Host, connectionInfo.Port, connectionInfo.Username, privateKeyFile);
                }
                else
                {
                    client = new SshClient(connectionInfo.Host, connectionInfo.Port, connectionInfo.Username, connectionInfo.Password);
                }
                
                // 设置连接超时
                client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
                client.Connect();
                client.KeepAliveInterval = TimeSpan.FromSeconds(30);
                
                _connections[key] = new ConnectionItem { Client = client, UsageCount = 1, LastUsed = DateTime.Now };
                return client;
            }
        }

        public void ReleaseConnection(SshClient client)
        {
            if (client == null)
                return;
                
            string key = $"{client.ConnectionInfo.Host}:{client.ConnectionInfo.Port}:{client.ConnectionInfo.Username}";
            
            lock (_lockObject)
            {
                if (_connections.TryGetValue(key, out var item))
                {
                    item.UsageCount--;
                    if (item.UsageCount <= 0)
                    {
                        try
                        {
                            client.Disconnect();
                            client.Dispose();
                        }
                        catch { }
                        _connections.Remove(key);
                    }
                }
            }
        }

        private class ConnectionItem
        {
            public SshClient? Client { get; set; }
            public int UsageCount { get; set; }
            public DateTime ConnectTime { get; set; } = DateTime.Now;
            public DateTime LastUsed { get; set; } = DateTime.Now;
        }

        private void CleanupIdleConnections(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_lockObject)
            {
                var now = DateTime.Now;
                var keysToRemove = new List<string>();
                var idleConnections = new List<KeyValuePair<string, ConnectionItem>>();

                foreach (var kvp in _connections)
                {
                    var key = kvp.Key;
                    var item = kvp.Value;

                    // 检查连接是否已断开
                    if (item.Client == null || !item.Client.IsConnected)
                    {
                        keysToRemove.Add(key);
                        continue;
                    }

                    // 检查连接是否空闲超时
                    if ((now - item.LastUsed).TotalMinutes > ConnectionTimeoutMinutes)
                    {
                        // 检查连接是否正在使用
                        if (item.UsageCount <= 0)
                        {
                            keysToRemove.Add(key);
                        }
                    }
                    else if (item.UsageCount <= 0)
                    {
                        // 收集空闲但未超时的连接
                        idleConnections.Add(kvp);
                    }
                }

                // 当连接池接近最大容量时，清理一些空闲连接
                if (_connections.Count > _maxConnections * 0.8 && idleConnections.Count > 0)
                {
                    // 按最后使用时间排序，优先清理最久未使用的
                    var sortedIdleConnections = idleConnections.OrderBy(kvp => kvp.Value.LastUsed).ToList();
                    
                    // 清理多余的空闲连接
                    int connectionsToRemove = _connections.Count - (int)(_maxConnections * 0.7);
                    for (int i = 0; i < Math.Min(connectionsToRemove, sortedIdleConnections.Count); i++)
                    {
                        var key = sortedIdleConnections[i].Key;
                        var item = sortedIdleConnections[i].Value;
                        
                        // 清理连接
                        try
                        {
                            if (item.Client != null && item.Client.IsConnected)
                            {
                                item.Client.Disconnect();
                                item.Client.Dispose();
                            }
                        }
                        catch { }

                        keysToRemove.Add(key);
                    }
                }

                // 移除需要清理的连接
                foreach (var key in keysToRemove)
                {
                    _connections.Remove(key);
                }
            }
        }

        public List<Windows.ConnectionStat> GetConnectionStats()
        {
            var stats = new List<Windows.ConnectionStat>();
            
            lock (_lockObject)
            {
                foreach (var kvp in _connections)
                {
                    var key = kvp.Key;
                    var item = kvp.Value;
                    
                    // 解析连接键，提取主机、端口和用户名
                    string[] parts = key.Split(':');
                    if (parts.Length >= 3)
                    {
                        string host = parts[0];
                        int port = int.TryParse(parts[1], out int p) ? p : 22;
                        string username = parts[2];
                        
                        // 检查连接状态
                        string status = item.Client != null && item.Client.IsConnected ? "已连接" : "已断开";
                        
                        stats.Add(new Windows.ConnectionStat
                        {
                            Key = key,
                            Host = host,
                            Port = port,
                            Username = username,
                            UsageCount = item.UsageCount,
                            ConnectTime = item.ConnectTime,
                            LastUsed = item.LastUsed,
                            Status = status
                        });
                    }
                }
            }
            
            return stats;
        }

        public int GetCurrentConnectionCount()
        {
            lock (_lockObject)
            {
                return _connections.Count;
            }
        }

        public int GetMaxConnections()
        {
            return _maxConnections;
        }
        
        /// <summary>
        /// 监控系统资源并动态调整连接池大小
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MonitorSystemResources(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                // 获取系统资源使用情况
                float cpuUsage = GetCpuUsage();
                float memoryUsage = GetMemoryUsage();
                
                // 根据资源使用情况调整目标最大连接数
                int newTargetMaxConnections = CalculateTargetMaxConnections(cpuUsage, memoryUsage);
                
                // 平滑调整连接池大小
                if (newTargetMaxConnections != _targetMaxConnections)
                {
                    _targetMaxConnections = newTargetMaxConnections;
                    // 逐步调整，避免突然变化
                    AdjustMaxConnectionsGradually();
                }
            }
            catch { }
        }
        
        /// <summary>
        /// 获取CPU使用率
        /// </summary>
        /// <returns>CPU使用率（0-100）</returns>
        private float GetCpuUsage()
        {
            try
            {
                // 简化实现，实际项目中可以使用PerformanceCounter
                // 这里返回一个模拟值，实际项目中需要使用性能计数器
                return 50.0f; // 模拟50%的CPU使用率
            }
            catch
            {
                return 50.0f; // 默认值
            }
        }
        
        /// <summary>
        /// 获取内存使用率
        /// </summary>
        /// <returns>内存使用率（0-100）</returns>
        private float GetMemoryUsage()
        {
            try
            {
                // 简化实现，实际项目中可以使用PerformanceCounter
                // 这里返回一个模拟值，实际项目中需要使用性能计数器
                return 60.0f; // 模拟60%的内存使用率
            }
            catch
            {
                return 60.0f; // 默认值
            }
        }
        
        /// <summary>
        /// 根据资源使用情况计算目标最大连接数
        /// </summary>
        /// <param name="cpuUsage">CPU使用率</param>
        /// <param name="memoryUsage">内存使用率</param>
        /// <returns>目标最大连接数</returns>
        private int CalculateTargetMaxConnections(float cpuUsage, float memoryUsage)
        {
            // 基础连接数
            int baseConnections = 10;
            
            // 根据CPU和内存使用率调整
            if (cpuUsage > 80 || memoryUsage > 80)
            {
                // 高负载，减少连接数
                return Math.Max(_minConnections, baseConnections / 2);
            }
            else if (cpuUsage > 60 || memoryUsage > 60)
            {
                // 中等负载，保持基础连接数
                return baseConnections;
            }
            else
            {
                // 低负载，增加连接数
                return Math.Min(30, baseConnections * 2);
            }
        }
        
        /// <summary>
        /// 逐步调整连接池大小
        /// </summary>
        private void AdjustMaxConnectionsGradually()
        {
            lock (_lockObject)
            {
                if (_maxConnections < _targetMaxConnections)
                {
                    // 增加连接数
                    _maxConnections = Math.Min(_maxConnections + 1, _targetMaxConnections);
                }
                else if (_maxConnections > _targetMaxConnections)
                {
                    // 减少连接数
                    _maxConnections = Math.Max(_maxConnections - 1, _minConnections);
                }
            }
        }
    }
}