using System;
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

            // Check the parameter 
            ValidateParameter("SRC_ACCOUNT_NAME");
            ValidateParameter("SRC_ACCOUNT_KEY");
            ValidateParameter("TGT_ACCOUNT_KEY");
            ValidateParameter("TGT_ACCOUNT_KEY");

            var srcAccountName = Environment.GetEnvironmentVariable("SRC_ACCOUNT_NAME");
            var srcAccountKey = Environment.GetEnvironmentVariable("SRC_ACCOUNT_KEY");
            var srcAccountEndpointSuffix = Environment.GetEnvironmentVariable("SRC_ACCOUNT_ENDPOINT_SUFFIX");

            var tgtAccountName = Environment.GetEnvironmentVariable("TGT_ACCOUNT_NAME");
            var tgtAccountKey = Environment.GetEnvironmentVariable("TGT_ACCOUNT_KEY");
            var tgtAccountEndpointSuffix = Environment.GetEnvironmentVariable("TGT_ACCOUNT_ENDPOINT_SUFFIX");

            // Information 
            Console.WriteLine($"           Account Name: {srcAccountName}");
            Console.WriteLine($"Account Endpoint-Suffix: {srcAccountEndpointSuffix}");

            // instantiate the logger
            var logger = new BackupStorageLogger();

            // build a storage context 
            using (var storageContext = new StorageContext(srcAccountName, srcAccountKey, srcAccountEndpointSuffix))
            {
                // build the cloud account 
                var backupStorageAccount = new CloudStorageAccount(new StorageCredentials(tgtAccountName, tgtAccountKey), tgtAccountEndpointSuffix, true);

                // instantiate the backup service 
                var backupService = new BackupService(storageContext, backupStorageAccount, logger);

                // exceute the backup 
                backupService.Backup("xx", null).Wait();
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
