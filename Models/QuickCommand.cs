namespace SimpleSshClient.Models
{
    public class QuickCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public int SortOrder { get; set; } = 0;
        public DateTime? LastExecutedAt { get; set; }
        public int ExecuteCount { get; set; } = 0;
    }
}
