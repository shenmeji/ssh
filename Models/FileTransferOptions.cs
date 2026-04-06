using System.Threading;

namespace SimpleSshClient.Models
{
    public class FileTransferOptions
    {
        public required string LocalPath { get; set; }
        public required string RemotePath { get; set; }
        public bool Overwrite { get; set; } = false;
        public CancellationToken CancellationToken { get; set; } = default;
        public Action<double>? ProgressCallback { get; set; }
    }
}
