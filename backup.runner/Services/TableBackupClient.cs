using System;
using System.Threading.Tasks;
using backup.runner.Manifest;
using CoreHelpers.WindowsAzure.Storage.Table;
using CoreHelpers.WindowsAzure.Storage.Table.Backup;
using Microsoft.Extensions.Logging;

namespace backup.runner.Services;

internal class TableBackupClient
{
    public static async Task BackupAsync(ManifestItem manifest, ILoggerFactory loggerFactory)
    {
        // build the logger
        var logger = loggerFactory.CreateLogger<TableBackupClient>();
        
        // verify the manifest parameters
        if (string.IsNullOrEmpty(manifest.TargetConnectionString))
            throw new InvalidOperationException("TargetConnectionString is not set");
        
        if (string.IsNullOrEmpty(manifest.TargetContainer))
            throw new InvalidOperationException("TargetContainer is not set");

        if (string.IsNullOrEmpty(manifest.SourceConnectionString))
            throw new InvalidOperationException("SourceConnectionString is not set");

        // dump the excludes
        if (manifest.Excludes.Count > 0)
        {
            logger.LogInformation("Excluded Tables:");
            foreach (var exclude in manifest.Excludes)
                logger.LogInformation($"\n{exclude}");
        }
        
        // build the backup prefix
        var prefix = DateTime.UtcNow.ToString("yyyy-MM-dd") + "-" + Guid.NewGuid().ToString();
         
        // instantiate the backup service
        var backupService = new BackupService(loggerFactory);
        
        // generate the backup context
        using var backupContext = await backupService.OpenBackupContext(manifest.TargetConnectionString, manifest.TargetContainer, prefix);

        // build the a storage table context
        using var storageContext = new StorageContext(manifest.SourceConnectionString);
        
        // trigger the backup
        await backupContext.Backup(storageContext, manifest.Excludes.ToArray());
        
        // done
        logger.LogInformation("Backup is finished");
    }

    public static async Task RestoreAsync(ManifestItem manifest, ILoggerFactory loggerFactory)
    {
        // build the logger
        var logger = loggerFactory.CreateLogger<TableBackupClient>();

        // verify the manifest parameters
        if (string.IsNullOrEmpty(manifest.TargetConnectionString))
            throw new InvalidOperationException("TargetConnectionString is not set");
        
        if (string.IsNullOrEmpty(manifest.SourceConnectionString))
            throw new InvalidOperationException("SourceConnectionString is not set");
        
        if (string.IsNullOrEmpty(manifest.SourceContainer))
            throw new InvalidOperationException("SourceContainer is not set");
        
        if (string.IsNullOrEmpty(manifest.SourcePath))
            throw new InvalidOperationException("SourcePath is not set");
        
        // log the backup id 
        logger.LogInformation("Restore from backup id {0}", manifest.SourcePath);
        
        // instantiate the backup service
        var backupService = new BackupService(loggerFactory);
        
        // generate the backup context
        using var backupContext = await backupService.OpenRestorContext(manifest.SourceConnectionString, manifest.SourceContainer, manifest.SourcePath);

        // build the a storage table context
        using var storageContext = new StorageContext(manifest.TargetConnectionString);
        
        // trigger the backup
        await backupContext.Restore(storageContext, manifest.Excludes.ToArray());
        
        // done
        logger.LogInformation("Restore is finished");
    }
}