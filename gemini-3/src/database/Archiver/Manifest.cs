using System.Collections.Generic;
public class ManifestRoot
{
    public Dictionary<string, ManifestEntry> TextContent { get; set; } = new();
}
public class ManifestEntry
{
    public string KeyName    { get; set; } = string.Empty;
    public string FileName   { get; set; } = string.Empty;
    public int    EntityCount{ get; set; }
}