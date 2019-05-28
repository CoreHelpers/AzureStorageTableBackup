using CoreHelpers.WindowsAzure.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.RetryPolicies;
using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace backup.runner
{
    public class BlobService
    {
        private readonly BlobContext _srcBlobContext;
        private readonly BlobContext _tgtBlobContext;
        private readonly BlobRequestOptions _options;
        private readonly IStorageLogger _logger;

        /// <summary>
        /// Implements backup and restore functions for blobs.
        /// </summary>
        /// <param name="srcStorageAccount"></param>
        /// <param name="tgtStorageAccount"></param>
        /// <param name="options"></param>
        /// <param name="logger"></param>
        public BlobService(
            CloudStorageAccount srcStorageAccount,
            CloudStorageAccount tgtStorageAccount,
            BlobRequestOptions options,
            IStorageLogger logger)
        {
            // set the contexts
            _srcBlobContext = new BlobContext(srcStorageAccount);
            _tgtBlobContext = new BlobContext(tgtStorageAccount);

            // set some rety oprions
            options.ServerTimeout = new TimeSpan(0, 180, 0);
            options.RetryPolicy = new ExponentialRetry(TimeSpan.Zero, 20);

            // disbale md5 check, we use https already
            options.DisableContentMD5Validation = true;

            _options = options;
            _logger = logger;
        }

        /// <summary>
        /// Restores all the blob container/blobs from the source blob container
        /// containing the backup.
        /// </summary>
        /// <param name="srcBlobContainerNameToRestoreFrom">Name of the backup container</param>
        /// <param name="backupId">Id of the backup to restore. Format is yyyy-mm-dd-guid.</param>
        /// <param name="areBackupBlobsCompressed">True if the backup used compression.</param>
        /// <param name="cToken"></param>
        /// <returns></returns>
        public async Task Restore(
            string srcBlobContainerNameToRestoreFrom,
            string backupId,
            bool areBackupBlobsCompressed = false,
            CancellationToken cToken = default)
        {
            try
            {
                _logger.LogInformation($"Restore backup from container {srcBlobContainerNameToRestoreFrom}.");

                // get the backup container to restore from
                CloudBlobContainer srcContainer = _srcBlobContext.GetBlobContainer(srcBlobContainerNameToRestoreFrom);
                BlobContinuationToken continuation = default;
                do
                {
                    // list all blobs in the backup container
                    var backupBlobList = await srcContainer.ListBlobsSegmentedAsync(
                            backupId, // load only blobs from the desired backup
                            useFlatBlobListing: true, // virtual file path prefixes the blobs
                            BlobListingDetails.All,
                            maxResults: 1000, // get 100 blobs at maximum per segment
                            continuation,
                            null,
                            null,
                            cToken)
                        .ConfigureAwait(false);

                    continuation = backupBlobList.ContinuationToken;
                    // cast IListBlobItem to CloudBlobs
                    var srcBlobs = backupBlobList.Results.Select(b => b as CloudBlob);

                    _logger.LogInformation($"Loaded #{srcBlobs.Count()} blobs from backup with ID {backupId}.");

                    // start to restore the container with the current segment of backup blobs
                    await RestoreBlobsInSegmentFromSourceBlobsToTargetBlobsAsync(
                            srcBlobs,
                            backupId,
                            areBackupBlobsCompressed,
                            cToken)
                        .ConfigureAwait(false);
                }
                while (continuation != null);

                _logger.LogInformation("Restore done.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Restores all blobs in the segment by copying the blobs to the target storage account and container.
        /// </summary>
        /// <param name="srcBlobs">All the blobs from the backup source container</param>
        /// <param name="backupId">Id of the backup to restore. Format is yyyy-mm-dd-guid.</param>
        /// <param name="areBackupBlobsCompressed">True if the backup used compression.</param>
        /// <param name="cToken"></param>
        /// <returns></returns>
        private async Task RestoreBlobsInSegmentFromSourceBlobsToTargetBlobsAsync(
            IEnumerable<CloudBlob> srcBlobs,
            string backupId,
            bool areBackupBlobsCompressed,
            CancellationToken cToken = default)
        {
            // create all original container from the source blobs
            IEnumerable<CloudBlobContainer> tgtBlobContainers =
                await CreateTgtBlobContainersFromBlobsAsync(srcBlobs).ConfigureAwait(false);

            _logger.LogInformation($"Created #{tgtBlobContainers.Count()} blob containers in total.");

            // loop through each container
            foreach (CloudBlobContainer tgtBlobContainer in tgtBlobContainers)
            {
                // select only blobs that belong to the current selected container
                var srcBlobsInContainer = srcBlobs.Where(s => s.Name.StartsWith(backupId + "/" + tgtBlobContainer.Name));
                if (areBackupBlobsCompressed) // if backup blobs are compressed, decompress them before the copy happens
                {
                    // if uploading compressed blobs, do it one-by-one
                    //
                    // using ParallelForEachAsync here can cause a BadRequest 400 (InvalidBlockId), because
                    // 2 tasks are trying to modify the same block from the blob
                    //
                    // note: my assumption is that it interferes with the ParallelOperationThreadCount option
                    // todo: Find a solution to maximize throughput
                    foreach (CloudBlob srcBlob in srcBlobsInContainer)
                    {
                        // compress each blob and copy it to the target container
                        await CopySourceBlobToTargetBlobUsingGzipStreamAsync(
                                srcBlob,
                                tgtBlobContainer,
                                backupId,
                                isBackup: false, // restore mode
                                cToken)
                            .ConfigureAwait(false);
                    }
                }
                else // if backup blobs are not compressed then start the copy
                {
                    // max 100 start cooy tasks & monitoring status of the copy
                    await srcBlobsInContainer.ParallelForEachAsync(
                            (srcBlob) =>
                                StartServerSideCopySourceBlobToTargetBlobAsync(
                                    srcBlob,
                                    tgtBlobContainer,
                                    backupId,
                                    isBackup: false, // restore mode
                                    cToken),
                            maxDegreeOfParalellism: 100,
                            cToken)
                        .ConfigureAwait(false);
                }

                _logger.LogInformation($"Restored #{srcBlobsInContainer.Count()} blobs in container {tgtBlobContainer.Name}.");

                // decompressing the blobs from the backup can take a lot of memory.
                // to avoid out of memory exceptions: force a gargabe collection after each container restore
                if (areBackupBlobsCompressed) GC.Collect();
            }
        }

        /// <summary>
        /// Creates the blob containers from blobs in collection.
        /// </summary>
        /// <param name="srcBlobs"></param>
        /// <returns></returns>
        private async Task<IEnumerable<CloudBlobContainer>> CreateTgtBlobContainersFromBlobsAsync(IEnumerable<CloudBlob> srcBlobs)
        {
            // get the container names
            IList<string> distinctTgtBlobContainerNames = new List<string>();
            foreach (CloudBlob srcBlob in srcBlobs)
            {
                // we need to determine the container name from the backup blob name
                // the name schema for a backup blob is:
                // yyyy-mm-dd-guid/original-container-name/virtual/file/path/blobname
                string targetContainerName = srcBlob.Name.Split("/")[1];
                if (!distinctTgtBlobContainerNames.Contains(targetContainerName))
                {
                    distinctTgtBlobContainerNames.Add(targetContainerName);
                    _logger.LogInformation($"Extracted blob container {targetContainerName} from loaded blobs.");
                }
            }

            ConcurrentBag<CloudBlobContainer> distinctTgtBlobContainer = new ConcurrentBag<CloudBlobContainer>();
            // create the container using a parallel for each
            await distinctTgtBlobContainerNames.ParallelForEachAsync
                    (
                        async (tgtBlobContainerName) =>
                        {
                            CloudBlobContainer tgtBlobContainer =
                                await _tgtBlobContext.CreateContainerIfNotExistsAsync(tgtBlobContainerName).ConfigureAwait(false);

                            _logger.LogInformation($"Created blob container {tgtBlobContainer.Name}.");

                            distinctTgtBlobContainer.Add(tgtBlobContainer);
                        },
                        maxDegreeOfParalellism: 0 // default means hardware based auto config
                    )
                .ConfigureAwait(false);

            // return the list of blob container
            return distinctTgtBlobContainer;
        }

        /// <summary>
        /// Backup all blob containers/blobs from the source account to the target blob container in
        /// the target account.
        /// </summary>
        /// <param name="targetBlobContainerName">the backup container name</param>
        /// <param name="virtualFilePath">prefix of the form yyyy-mm-dd-guid </param>
        /// <param name="excludedBlobContainers">blob container to exclude</param>
        /// <param name="compress"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        public async Task Backup(
            string targetBlobContainerName,
            string virtualFilePath,
            IEnumerable<string> excludedBlobContainers,
            bool compress = false,
            CancellationToken cToken = default)
        {
            try
            {
                BlobContinuationToken continuationToken = default;
                do
                {
                    var containerResultSegment = await _srcBlobContext
                        .BlobClient
                        .ListContainersSegmentedAsync(
                            string.Empty, // no prefix
                            ContainerListingDetails.None,
                            100, // max of 100 container segments returned
                            continuationToken,
                            null,
                            null,
                            cToken)
                        .ConfigureAwait(false);

                    continuationToken = containerResultSegment.ContinuationToken;
                    var srcContainersInSegment = containerResultSegment.Results;

                    // exclude blob containers from backup
                    srcContainersInSegment = srcContainersInSegment.Where(c => !excludedBlobContainers.Any(eb => c.Name == eb));

                    _logger.LogInformation($"Loaded #{srcContainersInSegment.Count()} blob containers to create a backup from.");

                    // copy the blobs from each src container to the backup container
                    // todo: can be improved with a parallel foreach
                    foreach (var srcBlobContainer in srcContainersInSegment)
                    {
                        _logger.LogInformation($"Start to backup container {srcBlobContainer.Name}.");

                        // start copying blobs
                        await CopyBlobsToTargetContainerAsync(
                                targetBlobContainerName,
                                srcBlobContainer,
                                virtualFilePath,
                                compress,
                                cToken)
                            .ConfigureAwait(false);
                    }
                }
                while (continuationToken != null);

                _logger.LogInformation("Backup done.");
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// Copy they blobs to the target backup container
        /// </summary>
        /// <param name="tgtBlobContainerName"></param>
        /// <param name="srcContainer"></param>
        /// <param name="virtualFilePath">yyyy-mm-dd-guid</param>
        /// <param name="compress"></param>
        /// <param name="cToken"></param>
        /// <returns></returns>
        private async Task CopyBlobsToTargetContainerAsync(
            string tgtBlobContainerName,
            CloudBlobContainer srcContainer,
            string virtualFilePath,
            bool compress = false,
            CancellationToken cToken = default)
        {
            // create a virtual directory for each container that is copied
            // use orignal container name as suffix
            virtualFilePath = virtualFilePath + "/" + srcContainer.Name;

            // get the target container
            CloudBlobContainer tgtContainer =
                await _tgtBlobContext.CreateContainerIfNotExistsAsync(tgtBlobContainerName).ConfigureAwait(false);

            BlobContinuationToken continuation = default;
            do
            {
                // load the blobs from the source container
                var blobListSegment = await srcContainer
                    .ListBlobsSegmentedAsync(
                        null,
                        useFlatBlobListing: true, // no virtual directories needed
                        BlobListingDetails.All,
                        maxResults: 1000, // get 100 blobs at maximum per segment
                        continuation,
                        null,
                        null,
                        cToken)
                    .ConfigureAwait(false);

                continuation = blobListSegment.ContinuationToken;
                // cast from IListBlobItem to cloudblob
                var srcBlobItems = blobListSegment.Results.Select(b => b as CloudBlob);

                if (compress)
                    _logger.LogInformation($"Loaded #{srcBlobItems.Count()} blobs from container {srcContainer.Name}." +
                                           $" Upload compressed blobs to target container {tgtContainer.Name}.");
                else
                    _logger.LogInformation($"Loaded #{srcBlobItems.Count()} blobs from container {srcContainer.Name}." +
                                           $" Start copying blobs to target container {tgtContainer.Name}.");

                // copy blobs in loaded segment to target container
                await CopyBlobsInSegementToTargetContainerAsync(
                        srcBlobItems,
                        tgtContainer,
                        virtualFilePath,
                        compress,
                        cToken)
                    .ConfigureAwait(false);
            }
            while (continuation != null);
        }

        /// <summary>
        /// Copies the source blobs in the segment to the target blobs.
        /// </summary>
        /// <param name="srcBlobItems"></param>
        /// <param name="tgtContainer"></param>
        /// <param name="virtualFilePath"></param>
        /// <param name="compress"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        private async Task CopyBlobsInSegementToTargetContainerAsync(
            IEnumerable<CloudBlob> srcBlobItems,
            CloudBlobContainer tgtContainer,
            string virtualFilePath,
            bool compress = false,
            CancellationToken ctoken = default)
        {
            if (compress)
            {
                // if uploading compressed blobs, do it one-by-one
                //
                // using ParallelForEachAsync here can cause a BadRequest 400 (InvalidBlockId), because
                // 2 tasks are trying to modify the same block from the blob
                //
                // note: my assumption is that it interferes with the ParallelOperationThreadCount option
                // todo: Find a solution to maximize throughput
                foreach (CloudBlob srcBlob in srcBlobItems)
                {
                    await CopySourceBlobToTargetBlobUsingGzipStreamAsync(
                            srcBlob,
                            tgtContainer,
                            virtualFilePath,
                            isBackup: true,
                            ctoken)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                // max 100 start cooy tasks & monitoring status of the copy
                await srcBlobItems.ParallelForEachAsync
                    (
                        (srcCloudBlob) =>
                            StartServerSideCopySourceBlobToTargetBlobAsync(
                                srcCloudBlob,
                                tgtContainer,
                                virtualFilePath,
                                isBackup: true,
                                ctoken),
                        maxDegreeOfParalellism: 100,
                        ctoken
                    )
                    .ConfigureAwait(false);
            }

            _logger.LogInformation($"Backup of #{srcBlobItems.Count()} blobs finished.");

            // compressing the source blobs can take a lot of memory.
            // to avoid out of memory exceptions: force a gargabe collection after each segment
            if (compress) GC.Collect();
        }

        /// <summary>
        /// Starts server side copying of the source blob to the target blob.
        /// </summary>
        /// <param name="srcCloudBlob"></param>
        /// <param name="tgtContainer"></param>
        /// <param name="virtualFilePath"></param>
        /// <param name="isBackup"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        private async Task StartServerSideCopySourceBlobToTargetBlobAsync(
             CloudBlob srcCloudBlob,
             CloudBlobContainer tgtContainer,
             string virtualFilePath,
             bool isBackup,
             CancellationToken ctoken = default)
        {
            // gets the target blob name dependent of the mode (backup or restore)
            var tgtBlobName = GetTgtBlobNameForMode(isBackup, srcCloudBlob.Name, virtualFilePath);
            // get the needed sastokens, without sastokens azure returns a 404
            var tgtBlob = GetTgtBlobWithSasUri(tgtContainer, tgtBlobName);
            var srcBlobSasUri = GetSrcBlobSasUri(srcCloudBlob);

            // start the copy task
            await tgtBlob.StartCopyAsync(srcBlobSasUri, null, null, _options, null, ctoken).ConfigureAwait(false);

            // check the status of the copy process
            while (tgtBlob.CopyState.Status == CopyStatus.Pending)
            {
                // wait a second before checking the status again
                await Task.Delay(1000).ConfigureAwait(false);
                await tgtBlob.FetchAttributesAsync().ConfigureAwait(false);
            }

            if (tgtBlob.CopyState.Status != CopyStatus.Success)
            {
                // TODO log it if needed
            }
        }

        /// <summary>
        /// Get the sas blob uri with read permissions. The sas uri is valid for one day.
        /// </summary>
        /// <param name="srcBlob"></param>
        /// <returns></returns>
        private Uri GetSrcBlobSasUri(CloudBlob srcBlob)
        {
            var readPolicy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = new DateTimeOffset(DateTime.UtcNow.AddDays(1))
            };

            var srcSasToken = srcBlob.GetSharedAccessSignature(readPolicy);
            return new Uri($"{srcBlob.Uri.AbsoluteUri}{srcSasToken}");
        }

        /// <summary>
        /// Get the target blob from a sas blob uri with read and write permissions.
        /// The sas uri is valid fo 1 day.
        /// </summary>
        /// <param name="tgtContainer"></param>
        /// <param name="tgtBlobName"></param>
        /// <returns></returns>
        private CloudBlob GetTgtBlobWithSasUri(CloudBlobContainer tgtContainer, string tgtBlobName)
        {
            var tgtBlob = tgtContainer.GetBlockBlobReference(tgtBlobName);

            var writePolicy = new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = new DateTimeOffset(DateTime.UtcNow.AddDays(1))
            };

            var tgtSasToken = tgtBlob.GetSharedAccessSignature(writePolicy);

            StorageCredentials credentials = new StorageCredentials(tgtSasToken);
            return new CloudBlockBlob(credentials.TransformUri(tgtBlob.Uri));
        }

        /// <summary>
        /// Stream blob through web server, compress it and stream back to target blob
        /// </summary>
        /// <param name="srcCloudBlob"></param>
        /// <param name="tgtContainer"></param>
        /// <param name="virtualFilePath"></param>
        /// <param name="isBackup"></param>
        /// <param name="ctoken"></param>
        /// <returns></returns>
        private async Task CopySourceBlobToTargetBlobUsingGzipStreamAsync(
            CloudBlob srcCloudBlob,
            CloudBlobContainer tgtContainer,
            string virtualFilePath,
            bool isBackup,
            CancellationToken cToken = default)
        {
            // get the target blob name dependent of the mode
            var tgtBlobName = GetTgtBlobNameForMode(isBackup, srcCloudBlob.Name, virtualFilePath);
            // append or remove .gz extension
            tgtBlobName = AppendOrRemoveGzipExtensionForMode(isBackup, tgtBlobName);

            if (isBackup)
            {
                // compress stream an upload it
                await UploadCompressedSrcBlobToTgtBlobAsnyc(srcCloudBlob, tgtContainer, tgtBlobName, cToken).ConfigureAwait(false);
            }
            else
            {
                // decompress stream and upload it
                await UploadDecompressedSrcBlobToTgtBlobAsnyc(srcCloudBlob, tgtContainer, tgtBlobName, cToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uploads the compressed blob.
        /// </summary>
        /// <param name="srcBlob"></param>
        /// <param name="tgtContainer"></param>
        /// <param name="tgtBlobName"></param>
        /// <param name="cToken"></param>
        /// <returns></returns>
        private async Task UploadCompressedSrcBlobToTgtBlobAsnyc(
            CloudBlob srcBlob,
            CloudBlobContainer tgtContainer,
            string tgtBlobName,
            CancellationToken cToken = default)
        {
            using (Stream blobStream = await srcBlob.OpenReadAsync(null, _options, null, cToken))
            using (MemoryStream streamToUplaod = new MemoryStream())
            using (var compressedBloblStream = new GZipStream(streamToUplaod, CompressionMode.Compress, leaveOpen: true))
            {
                await blobStream.CopyToAsync(compressedBloblStream).ConfigureAwait(false);
                compressedBloblStream.Close();
                streamToUplaod.Seek(0, SeekOrigin.Begin);
                var targetBlob = tgtContainer.GetBlockBlobReference(tgtBlobName);
                // save the original content type
                targetBlob.Metadata["contenttype"] = string.IsNullOrWhiteSpace(srcBlob.Properties?.ContentType)
                    ? "application/octet-stream".ToBase64()  // azure default content type
                    : srcBlob.Properties.ContentType.ToBase64();
                await targetBlob.UploadFromStreamAsync(streamToUplaod, null, _options, null, cToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Uploads a decompressed blob.
        /// </summary>
        /// <param name="srcBlob"></param>
        /// <param name="tgtContainer"></param>
        /// <param name="tgtBlobName"></param>
        /// <param name="cToken"></param>
        /// <returns></returns>
        private async Task UploadDecompressedSrcBlobToTgtBlobAsnyc(
            CloudBlob srcBlob,
            CloudBlobContainer tgtContainer,
            string tgtBlobName,
            CancellationToken cToken = default)
        {
            using (Stream blobStream = await srcBlob.OpenReadAsync(null, _options, null, cToken))
            using (var compressedBloblStream = new GZipStream(blobStream, CompressionMode.Decompress, leaveOpen: true))
            using (MemoryStream streamToUplaod = new MemoryStream())
            {
                await compressedBloblStream.CopyToAsync(streamToUplaod).ConfigureAwait(false);
                compressedBloblStream.Close();
                streamToUplaod.Seek(0, SeekOrigin.Begin);
                var targetBlob = tgtContainer.GetBlockBlobReference(tgtBlobName);
                // set the content type of the original blob
                targetBlob.Properties.ContentType = srcBlob.Metadata["contenttype"].FromBase64();
                await targetBlob.UploadFromStreamAsync(streamToUplaod, null, _options, null, cToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Get the target blob name depending on mode (backup or restore)
        /// </summary>
        /// <param name="isBackup"></param>
        /// <param name="srcBlobName"></param>
        /// <param name="virtualFilePath"></param>
        /// <returns></returns>
        private string GetTgtBlobNameForMode(bool isBackup, string srcBlobName, string virtualFilePath)
        {
            return isBackup
                ? GetBackupBlobName(srcBlobName, virtualFilePath)
                : GetOriginalBlobName(srcBlobName, virtualFilePath); // virtualFilePath is the backupid
        }

        /// <summary>
        /// Appends or removes the .gz extension.
        /// </summary>
        /// <param name="isBackup"></param>
        /// <param name="tgtBlobName"></param>
        /// <returns></returns>
        private string AppendOrRemoveGzipExtensionForMode(bool isBackup, string tgtBlobName)
        {
            return isBackup
                ? tgtBlobName + ".gz"
                : tgtBlobName.Remove(tgtBlobName.Length - 3);
        }

        /// <summary>
        /// Prefixes blob name with the virtual file path.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="virtualFilePath"></param>
        /// <returns></returns>
        private string GetBackupBlobName(string name, string virtualFilePath) => $"{virtualFilePath}/{name}";

        /// <summary>
        /// Extracts the original filepath and blob name from the
        /// backup file path and name using the backup id.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="backupId"></param>
        /// <returns></returns>
        private string GetOriginalBlobName(string name, string backupId) => name.Split(backupId + "/").Last().Split("/", 2)[1];
    }
}