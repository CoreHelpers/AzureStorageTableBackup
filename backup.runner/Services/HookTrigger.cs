using System;
using System.Net.Http;
using System.Threading.Tasks;
using backup.runner.Manifest;
using Newtonsoft.Json;

namespace backup.runner.Services;

internal static class HookTrigger
{
    public static async Task TriggerHookAsync(string hook)
    {
        var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(hook);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to trigger finished hook {response.ReasonPhrase}");
    }
}