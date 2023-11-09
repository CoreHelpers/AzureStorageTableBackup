using System.Collections.Generic;

namespace backup.runner.Manifest;

/// <summary>
/// The manifest document is a collection of manifest items describing a concrete backup or restore operation.
/// </summary>
public class ManifestDocument
{
    public string Id { get; set; } = string.Empty;
    
    public List<ManifestItem> Items { get; set; } = new List<ManifestItem>();
}