using System;

namespace backup.runner.Extensions;

internal static class EnvironmentEx
{
    public static string GetManifestLocation()
    {
        string manifest = Environment.GetEnvironmentVariable("MANIFEST");
        return string.IsNullOrWhiteSpace(manifest) ? string.Empty : manifest.Trim();
    }
}