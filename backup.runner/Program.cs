using System;
using backup.runner.Manifest;
using backup.runner.Services;
using Microsoft.Extensions.Logging;

// establish the logger
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
});

// create the standard logger
var logger = loggerFactory.CreateLogger<Program>();

// welcome Message
logger.LogInformation("Backup Client for Azure Storage Account Tables and Blobs");
logger.LogInformation("========================================================");

// read the manifest document 
var manifest = await ManifestAccessor.ReadManifest();
logger.LogInformation($"Manifest contains {manifest.Items.Count} tasks");

// process all tasks
foreach (var manifestItem in manifest.Items)
{
    if (!manifestItem.Enabled)
    {
        logger.LogInformation(
            $"Task {manifestItem.Name} ({manifestItem.Id}) - {manifestItem.Operation} {manifestItem.Storage} is disabled");
        continue;
    }

    // execute the operation
    logger.LogInformation($"Processing task {manifestItem.Name} ({manifestItem.Id}) - {manifestItem.Operation} {manifestItem.Storage}");
    if (manifestItem.Operation == OperationType.Backup && manifestItem.Storage == StorageType.Table)
    {
        // backup 
        await TableBackupClient.BackupAsync(manifestItem, loggerFactory);
        
        // trigger the finished hook if needed
        if (!String.IsNullOrEmpty(manifestItem.FinishedHook)) 
            await HookTrigger.TriggerHookAsync(manifestItem.FinishedHook);
        
    } else if (manifestItem.Operation == OperationType.Restore && manifestItem.Storage == StorageType.Table)
    {
        await TableBackupClient.RestoreAsync(manifestItem, loggerFactory);
    }
    else
    {
        throw new InvalidOperationException(
            $"Operation {manifestItem.Operation} or storage {manifestItem.Storage} is not supported");
    }


} 