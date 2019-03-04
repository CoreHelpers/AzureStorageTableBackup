using System;
using System.Collections.Generic;
using CoreHelpers.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;

namespace backup.runner
{
    class Program
    {
        static void Main(string[] args)
        {
            // Welcome Message
            Console.WriteLine("Backup Client for Azure Storage Account Tables");
            Console.WriteLine("");

            // Check the global parameters 
            ValidateParameter("SRC_ACCOUNT_NAME");
            ValidateParameter("SRC_ACCOUNT_KEY");
            ValidateParameter("TGT_ACCOUNT_KEY");
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
            Console.WriteLine($"Account Endpoint-Suffix: {srcAccountEndpointSuffix}");

            var operationsMode = Environment.GetEnvironmentVariable("MODE");
            if (operationsMode != null && operationsMode.ToLower().Equals("restore"))
            {
                // check the local parameters 
                ValidateParameter("SRC_ACCOUNT_CONTAINER");
                ValidateParameter("SRC_BACKUP_ID");

                // load the local parameters
                var srcAccountContainer = Environment.GetEnvironmentVariable("SRC_ACCOUNT_CONTAINER");
                var srcBackupId = Environment.GetEnvironmentVariable("SRC_BACKUP_ID");

                // log
                Console.WriteLine($"        Operations Mode: Restore");

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
                    backupService.Restore(srcAccountContainer, srcBackupId).Wait();
                }

                // Thank you 
                Console.WriteLine("Restore is finished");
            }
            else
            {
                // check the local parameters 
                ValidateParameter("TGT_ACCOUNT_CONTAINER");

                // load the local parameters
                var tgtAccountContainer = Environment.GetEnvironmentVariable("TGT_ACCOUNT_CONTAINER");
                var srcExcludeTables    = Environment.GetEnvironmentVariable("SRC_EXCLUDE_TABLES");

                // process exclude tables
                var excludeTablesList = new List<string>();
                if (!String.IsNullOrEmpty(srcExcludeTables))
                    excludeTablesList = new List<string>(srcExcludeTables.Split(','));

                // log
                Console.WriteLine($"        Operations Mode: Backup");

                if (excludeTablesList.Count == 0)
                {
                    Console.WriteLine($"        Excluded Tables: n/a");
                }
                else
                {
                    for (int i = 0; i < excludeTablesList.Count; i++) {
                        if (i == 0) 
                            Console.WriteLine($"        Excluded Tables: {excludeTablesList[i]}");
                        else
                            Console.WriteLine($"                         {excludeTablesList[i]}");
                    }
                }

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
                    var prefix = DateTime.Now.ToString("yyyy-MM-dd") + "-" + Guid.NewGuid().ToString();

                    // log
                    Console.WriteLine($"          Backup Prefix: {prefix}");

                    // exceute the backup 
                    backupService.Backup(tgtAccountContainer, prefix, excludeTablesList.ToArray()).Wait();
                }

                // Thank you 
                Console.WriteLine("Backup is finished");
            }
        }

        static void ValidateParameter(string variable) {

            var value = Environment.GetEnvironmentVariable(variable);
            if (String.IsNullOrEmpty(value))
            {
                var msg = $"ERROR: Missing {variable} environment variable";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(msg);
                Console.ResetColor();

                throw new Exception(msg);
            }
        }
    }
}
