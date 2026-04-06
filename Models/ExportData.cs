using System.Collections.Generic;

namespace SimpleSshClient.Models
{
    public class ExportData
    {
        // 元数据信息
        public ExportMetadata? Metadata { get; set; }
        
        // 连接列表
        public List<ConnectionInfo>? Connections { get; set; }
    }
}