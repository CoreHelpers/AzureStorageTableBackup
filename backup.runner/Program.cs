using CoreHelpers.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backup.runner
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // Welcome Message
            Console.WriteLine("Backup Client for Azure Storage Account Tables and Blobs");
            Console.WriteLine("");

            // Check the global parameters
            ValidateParameter("SRC_ACCOUNT_NAME");
            ValidateParameter("SRC_ACCOUNT_KEY");
            ValidateParameter("TGT_ACCOUNT_NAME");
            ValidateParameter("TGT_ACCOUNT_KEY");

            // Load the global parameters
            var srcAccountName = Environment.GetEnvironmentVariable("SRC_ACCOUNT_NAME");
            var srcAccountKey = Environment.GetEnvironmentVariable("SRC_ACCOUNT_KEY");
            var srcAccountEndpointSuffix = Environment.GetEnvironmentVariable("SRC_ACCOUNT_ENDPOINT_SUFFIX");

            var tgtAccountName = Environment.GetEnvironmentVariable("TGT_ACCOUNT_NAME");
            var tgtAccountKey = Environment.GetEnvironmentVariable("TGT_ACCOUNT_KEY");
            var tgtAccountEndpointSuffix = Environment.GetEnvironmentVariable("TGT_ACCOUNT_ENDPOINT_SUFFIX");

            // Information
            Console.WriteLine($"           Account Name: {srcAccountName}");
            Console.WriteLine($"           Account Endpoint-Suffix: {srcAccountEndpointSuffix}");

            var storageType = GetStorageTypeOrDefault();
            var operationsMode = GetOperationsModeOrDefault();

            if (IsTableRestoreMode(storageType, operationsMode))
            {
                // check the local parameters
                ValidateParameter("SRC_ACCOUNT_CONTAINER");
                ValidateParameter("SRC_BACKUP_ID");

                // load the local parameters
                var srcAccountContainer = Environment.GetEnvironmentVariable("SRC_ACCOUNT_CONTAINER");
                var srcBackupId = Environment.GetEnvironmentVariable("SRC_BACKUP_ID");

                // log
                Console.WriteLine($"           Storage Type: Table");
                Console.WriteLine($"           Operations Mode: Restore");

                // instantiate the logger
                var logger = new BackupStorageLogger();

                // build a storage context
                using (var storageContext = new StorageContext(tgtAccountName, tgtAccountKey, tgtAccountEndpointSuffix))
                {
                    // build the cloud account
                    var backupStorageAccount = new CloudStorageAccount(new StorageCredentials(srcAccountName, srcAccountKey), srcAccountEndpointSuffix, true);

                    // instantiate the backup service
                    var backupService = new BackupService(storageContext, backupStorageAccount, logger);

                    // exceute the backup
                    await backupService.Restore(srcAccountContainer, srcBackupId).ConfigureAwait(false);
                }

                // Thank you
                Console.WriteLine("Restore is finished");
            }
            else if (IsTableBackupMode(storageType, operationsMode))
            {
                // check the local parameters
                ValidateParameter("TGT_ACCOUNT_CONTAINER");

                // load the local parameters
                var tgtAccountContainer = Environment.GetEnvironmentVariable("TGT_ACCOUNT_CONTAINER");
                var srcExcludeTables = Environment.GetEnvironmentVariable("SRC_EXCLUDE_TABLES");

                // process exclude tables
                var excludeTablesList = new List<string>();
                if (!string.IsNullOrEmpty(srcExcludeTables))
                    excludeTablesList = new List<string>(srcExcludeTables.Split(','));

                PrintExcludedStorage(excludeTablesList, tables: true);

                // log
                Console.WriteLine($"           Storage Type: Table");
                Console.WriteLine($"           Operations Mode: Backup");

                // instantiate the logger
                var logger = new BackupStorageLogger();

                // build a storage context
                using (var storageContext = new StorageContext(srcAccountName, srcAccountKey, srcAccountEndpointSuffix))
                {
                    // build the cloud account
                    var backupStorageAccount = new CloudStorageAccount(new StorageCredentials(tgtAccountName, tgtAccountKey), tgtAccountEndpointSuffix, true);

                    // instantiate the backup service
                    var backupService = new BackupService(storageContext, backupStorageAccount, logger);

                    // build the backup prefix
                    var prefix = DateTime.UtcNow.ToString("yyyy-MM-dd") + "-" + Guid.NewGuid().ToString();

                    // log
                    Console.WriteLine($"           Backup Prefix: {prefix}");

                    // exceute the backup
                    await backupService.Backup(tgtAccountContainer, prefix, excludeTablesList.ToArray()).ConfigureAwait(false);
                }

                // Thank you
                Console.WriteLine("Backup is finished");
            }
            else if (IsBlobRestoreMode(storageType, operationsMode))
            {
                // check the local parameters
                ValidateParameter("SRC_ACCOUNT_CONTAINER");
                ValidateParameter("SRC_BACKUP_ID");

                // load the local parameters
                var srcAccountContainer = Environment.GetEnvironmentVariable("SRC_ACCOUNT_CONTAINER");
                var srcBackupId = Environment.GetEnvironmentVariable("SRC_BACKUP_ID");

                // load option parameters
                int parallelOperationThreadCount = GetParallelOperationThreadCountVariable();
                long singleBlobUploadThresholdInBytes = GetSingleBlobUploadThresholdInBytesVariable();
                bool areBlobsCompressed = GetBlobCompressionVariable();

                // log
                Console.WriteLine($"           Storage Type: Blob");
                Console.WriteLine($"           Operations Mode: Restore");

                // instantiate the logger
                var logger = new BackupStorageLogger();

                var srcBlobStorageAccount = new CloudStorageAccount(new StorageCredentials(srcAccountName, srcAccountKey), srcAccountEndpointSuffix, true);
                var tgtBlobStorageAccount = new CloudStorageAccount(new StorageCredentials(tgtAccountName, tgtAccountKey), tgtAccountEndpointSuffix, true);

                var blobRequestOptions = new BlobRequestOptions
                {
                    ParallelOperationThreadCount = parallelOperationThreadCount, // 10 simultanously uploaded blocks
                    SingleBlobUploadThresholdInBytes = singleBlobUploadThresholdInBytes // block size of 32 MB is default
                };

                Console.WriteLine($"           ParallelOperationThreadCount: {parallelOperationThreadCount}");
                Console.WriteLine($"           SingleBlobUploadThresholdInBytes: {singleBlobUploadThresholdInBytes}");

                // instantiate the backup service
                var backupService = new BlobService(srcBlobStorageAccount, tgtBlobStorageAccount, blobRequestOptions, logger);

                // exceute the backup
                await backupService.Restore(srcAccountContainer, srcBackupId, areBlobsCompressed).ConfigureAwait(false);
            }
            else if (IsBlobBackupMode(storageType, operationsMode))
            {
                // check the local parameters
                ValidateParameter("TGT_ACCOUNT_CONTAINER");

                int parallelOperationThreadCount = GetParallelOperationThreadCountVariable();
                long singleBlobUploadThresholdInBytes = GetSingleBlobUploadThresholdInBytesVariable();
                bool compressBlobs = GetBlobCompressionVariable();

                // load the local parameters
                var tgtAccountContainer = Environment.GetEnvironmentVariable("TGT_ACCOUNT_CONTAINER");
                var srcExcludedBlobContainers = Environment.GetEnvironmentVariable("SRC_EXCLUDE_BLOB_CONTAINER");

                // process exclude tables
                var excludedBlobContainers = new List<string>();
                if (!string.IsNullOrEmpty(srcExcludedBlobContainers))
                    excludedBlobContainers = new List<string>(srcExcludedBlobContainers.Split(','));

                PrintExcludedStorage(excludedBlobContainers, tables: false);

                // log
                Console.WriteLine($"           Storage Type: Blob");
                Console.WriteLine($"           Operations Mode: Backup");

                // instantiate the logger
                var logger = new BackupStorageLogger();

                var srcBlobStorageAccount = new CloudStorageAccount(new StorageCredentials(srcAccountName, srcAccountKey), srcAccountEndpointSuffix, true);
                var tgtBlobStorageAccount = new CloudStorageAccount(new StorageCredentials(tgtAccountName, tgtAccountKey), tgtAccountEndpointSuffix, true);

                var blobRequestOptions = new BlobRequestOptions
                {
                    ParallelOperationThreadCount = parallelOperationThreadCount, // 10 simultanously uploaded blocks
                    SingleBlobUploadThresholdInBytes = singleBlobUploadThresholdInBytes // block size of 32 MB is default
                };

                Console.WriteLine($"           ParallelOperationThreadCount: {parallelOperationThreadCount}");
                Console.WriteLine($"           SingleBlobUploadThresholdInBytes: {singleBlobUploadThresholdInBytes}");

                // instantiate the backup service
                var backupService = new BlobService(srcBlobStorageAccount, tgtBlobStorageAccount, blobRequestOptions, logger);

                // build the backup prefix
                var virtualFilePath = DateTime.UtcNow.ToString("yyyy-MM-dd") + "-" + Guid.NewGuid().ToString();

                // exceute the backup
                await backupService.Backup(tgtAccountContainer, virtualFilePath, excludedBlobContainers, compressBlobs).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine("Storage type and operations mode invalid.");
            }
        }

        private static bool IsTableRestoreMode(string storageType, string operationsMode)
            => operationsMode != null && storageType != null && storageType.ToLower().Equals("table") && operationsMode.ToLower().Equals("restore");

        private static bool IsTableBackupMode(string storageType, string operationsMode)
            => operationsMode != null && storageType != null && storageType.ToLower().Equals("table") && operationsMode.ToLower().Equals("backup");

        private static bool IsBlobBackupMode(string storageType, string operationsMode)
            => operationsMode != null && storageType != null && storageType.ToLower().Equals("blob") && operationsMode.ToLower().Equals("backup");

        private static bool IsBlobRestoreMode(string storageType, string operationsMode)
            => operationsMode != null && storageType != null && storageType.ToLower().Equals("blob") && operationsMode.ToLower().Equals("restore");

        private static void ValidateParameter(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(value))
            {
                var msg = $"ERROR: Missing {variable} environment variable";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();

                throw new Exception(msg);
            }
        }

        private static int GetParallelOperationThreadCountVariable()
        {
            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRD_COUNT"))
                    ? 10
                    : CastToIntType("TRD_COUNT", Environment.GetEnvironmentVariable("TRD_COUNT"));
        }

        private static long GetSingleBlobUploadThresholdInBytesVariable()
        {
            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UPL_THRESHOLD"))
                ? 30L * 1024L * 1024L
                : CastToLongType("UPL_THRESHOLD", Environment.GetEnvironmentVariable("UPL_THRESHOLD"));
        }

        private static bool GetBlobCompressionVariable()
        {
            return string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COMPRESS"))
                ? true
                : CastToBoolType("COMPRESS", Environment.GetEnvironmentVariable("COMPRESS"));
        }

        private static bool CastToBoolType(string varName, string varValue)
        {
            bool success = bool.TryParse(varValue.ToLower(), out bool var);
            if (!success)
            {
                PrintCastErrorMessageAndThrowException(varName, varValue);
            }

            return var;
        }

        private static long CastToLongType(string varName, string varValue)
        {
            bool success = long.TryParse(varValue, out long var);
            if (!success)
            {
                PrintCastErrorMessageAndThrowException(varName, varValue);
            }

            return var;
        }

        private static int CastToIntType(string varName, string varValue)
        {
            bool success = int.TryParse(varValue, out int var);
            if (!success)
            {
                PrintCastErrorMessageAndThrowException(varName, varValue);
            }

            return var;
        }

        private static string GetOperationsModeOrDefault()
        {
            string operationsMode = Environment.GetEnvironmentVariable("MODE");
            return string.IsNullOrWhiteSpace(operationsMode) ? "backup" : operationsMode;
        }

        private static string GetStorageTypeOrDefault()
        {
            string storageType = Environment.GetEnvironmentVariable("STORAGE_TYPE");
            return string.IsNullOrWhiteSpace(storageType) ? "table" : storageType;
        }

        private static void PrintCastErrorMessageAndThrowException(string varName, string varValue)
        {
            var msg = $"ERROR: Environment variable {varName} with value {varValue} could not be parsed to type bool.";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            Console.ResetColor();

            throw new Exception(msg);
        }

        private static void PrintExcludedStorage(List<string> excludedStorageList, bool tables = true)
        {
            string storageType = tables ? "Tables" : "Blobs";
            if (excludedStorageList.Count == 0)
            {
                Console.WriteLine($"           Excluded {storageType}: n/a");
            }
            else
            {
                for (int i = 0; i < excludedStorageList.Count; i++)
                {
                    if (i == 0)
                        Console.WriteLine($"           Excluded {storageType}: {excludedStorageList[i]}");
                    else
                        Console.WriteLine($"                      {excludedStorageList[i]}");
                }
            }
        }
    }
}