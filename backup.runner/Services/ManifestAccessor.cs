using System;
using System.IO;
using System.Threading.Tasks;
using backup.runner.Extensions;
using backup.runner.Manifest;
using Newtonsoft.Json;

namespace backup.runner.Services;

internal static class ManifestAccessor
{
    public static async Task<ManifestDocument> ReadManifest()
    {
        // get the manifest location 
        var location = EnvironmentEx.GetManifestLocation();
        if (string.IsNullOrEmpty(location))
            throw new InvalidOperationException("Manifest location is not set");

        // check if the file exists and if so parse the content
        if (File.Exists(location))
            return JsonConvert.DeserializeObject<ManifestDocument>(await File.ReadAllTextAsync(location));
            
        // TODO: check if the location is an url and we need to download the manifest
        
        await Task.CompletedTask;
        throw new NotImplementedException();
    }
}