using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using backup.runner.Extensions;
using backup.runner.Manifest;
using Microsoft.VisualBasic.CompilerServices;
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
        
        // check if a download is required 
        if (location.ToLower().StartsWith("http"))
        {
            var httpClient = new HttpClient();
            using var response = await httpClient.GetAsync(location);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to download manifest file {response.ReasonPhrase}");
            var jsonData = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ManifestDocument>(jsonData);
        }
        
        // in all other cases throw an exception
        throw new InvalidOperationException($"Not supported manifest location {location}");
    }
}