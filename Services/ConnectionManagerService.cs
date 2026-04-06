using System;
using System.Collections.Generic;
using System.Timers;
using SimpleSshClient.Models;

namespace SimpleSshClient.Services
{
    public class ConnectionManagerService
    {
        private static ConnectionManagerService? _instance;
        private readonly List<ConnectionInfo> _connections = new();
        private readonly List<SshService> _sshServices = new();
        private readonly List<DateTime> _lastUsedTimes = new();
        private readonly System.Timers.Timer _cleanupTimer;
        private const int ConnectionTimeoutMinutes = 30;

        public static ConnectionManagerService Instance => _instance ??= new ConnectionManagerService();

        public event EventHandler? ConnectionsChanged;
        public event EventHandler<ConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        public ConnectionManagerService()
        {
            // 初始化清理定时器，每5分钟检查一次
            _cleanupTimer = new System.Timers.Timer(5 * 60 * 1000);
            _cleanupTimer.Elapsed += CleanupIdleConnections;
            _cleanupTimer.Start();
        }

        public void AddConnection(ConnectionInfo connection, SshService sshService)
        {
            _connections.Add(connection);
            _sshServices.Add(sshService);
            _lastUsedTimes.Add(DateTime.Now);
            ConnectionsChanged?.Invoke(this, EventArgs.Empty);
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(connection, true));
        }

        public void RemoveConnection(SshService sshService)
        {
            var index = _sshServices.IndexOf(sshService);
            if (index >= 0)
            {
                var connection = _connections[index];
                _sshServices.RemoveAt(index);
                _connections.RemoveAt(index);
                _lastUsedTimes.RemoveAt(index);
                ConnectionsChanged?.Invoke(this, EventArgs.Empty);
                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusChangedEventArgs(connection, false));
            }
        }

        public List<ConnectionInfo> GetConnections()
        {
            return new List<ConnectionInfo>(_connections);
        }

        public List<SshService> GetSshServices()
        {
            return new List<SshService>(_sshServices);
        }

        public SshService GetSshService(int index)
        {
            if (index >= 0 && index < _sshServices.Count)
            {
                _lastUsedTimes[index] = DateTime.Now;
                return _sshServices[index];
            }
            return null;
        }

        public ConnectionInfo GetConnection(int index)
        {
            if (index >= 0 && index < _connections.Count)
            {
                return _connections[index];
            }
            return null;
        }

        public bool IsConnectionValid(SshService sshService)
        {
            var index = _sshServices.IndexOf(sshService);
            if (index >= 0)
            {
                // 同时检查SSH和SFTP的连接状态
                return sshService.IsConnected && sshService.IsSftpConnected;
            }
            return false;
        }

        public void UpdateLastUsedTime(SshService sshService)
        {
            var index = _sshServices.IndexOf(sshService);
            if (index >= 0)
            {
                _lastUsedTimes[index] = DateTime.Now;
            }
        }

        public ConnectionInfo? GetConnectionInfo(SshService sshService)
        {
            var index = _sshServices.IndexOf(sshService);
            if (index >= 0 && index < _connections.Count)
            {
                return _connections[index];
            }
            return null;
        }

        private void CleanupIdleConnections(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var toRemove = new List<int>();

            for (int i = 0; i < _lastUsedTimes.Count; i++)
            {
                if ((now - _lastUsedTimes[i]).TotalMinutes >= ConnectionTimeoutMinutes)
                {
                    toRemove.Add(i);
                }
            }

            // 从后往前移除，避免索引变化
            for (int i = toRemove.Count - 1; i >= 0; i--)
            {
                var index = toRemove[i];
                var sshService = _sshServices[index];
                if (sshService.IsConnected)
                {
                    try
                    {
                        sshService.Disconnect();
                    }
                    catch { }
                }
                RemoveConnection(sshService);
            }
        }
    }

    public class ConnectionStatusChangedEventArgs : EventArgs
    {
        public ConnectionInfo Connection { get; }
        public bool IsConnected { get; }

        public ConnectionStatusChangedEventArgs(ConnectionInfo connection, bool isConnected)
        {
            Connection = connection;
            IsConnected = isConnected;
        }
    }
}
