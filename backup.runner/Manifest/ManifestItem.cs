using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace backup.runner.Manifest;

public enum OperationType
{
    Backup,
    Restore
}

public enum StorageType
{
    Table
}

/// <summary>
/// Describes a specific backup operation
/// </summary>
public class ManifestItem
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public bool Enabled { get; set; } = true;
    
    [JsonConverter(typeof(StringEnumConverter))]
    public OperationType Operation { get; set; }
    [JsonConverter(typeof(StringEnumConverter))]
    public StorageType Storage { get; set; }

    public string TargetConnectionString { get; set; } = string.Empty;
    public string TargetContainer { get; set; } = string.Empty;

    public string SourceConnectionString { get; set; } = string.Empty;
    public string SourceContainer { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    
    public List<string> Excludes { get; set; } = new List<string>();
}