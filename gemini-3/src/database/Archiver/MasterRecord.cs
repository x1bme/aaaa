using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Archiver
{
    [JsonObject]
    public class MasterRecord
    {
    [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode" )]
    public int ArchiveVersion { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode" )]
    public int OriginDatabaseRevision { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode" )]
    public int OriginSoftwareVersion { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode" )]
    public long TimeStamp { get; set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode" )]
    // Used to double check the encryption.
    public string Key { get; set; }

    [JsonProperty("TextContent")]
    public Dictionary<string, FileRecord> TextContent { get; set; } = new Dictionary<string, FileRecord>();

    public MasterRecord() { Key = string.Empty; }
    }

    public class FileRecord
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode" )]
        public string? KeyName { get; set; }
        public string? FileName { get; set; }
        public int EntityCount { get; set; }

        [JsonIgnore]
        public string Content { get; set; }
    }
}

