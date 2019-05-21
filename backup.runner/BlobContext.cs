using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Threading.Tasks;

namespace backup.runner
{
    public class BlobContext
    {
        public CloudBlobClient BlobClient { get; private set; }

        public BlobContext(CloudStorageAccount storageAccount)
        {
            BlobClient = storageAccount.CreateCloudBlobClient();
        }

        public CloudBlobContainer GetBlobContainer(string name) => BlobClient.GetContainerReference(name);

        public async Task<CloudBlobContainer> CreateContainerIfNotExistsAsync(string containerName)
        {
            CloudBlobContainer blobContainer = GetBlobContainer(containerName);
            await blobContainer.CreateIfNotExistsAsync().ConfigureAwait(false);
            return blobContainer;
        }
    }
}