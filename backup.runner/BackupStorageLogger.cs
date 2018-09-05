using System;
using CoreHelpers.WindowsAzure.Storage.Table;

namespace backup.runner
{
    public class BackupStorageLogger : DefaultStorageLogger
    {
        public override void LogInformation(string text)
        {
            Console.WriteLine(text);
        }
    }
}
