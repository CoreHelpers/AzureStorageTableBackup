# Status

| Build | Status | 
|---|---|
| CI (last changed branch) |[![Build Status](https://applimit.visualstudio.com/CoreHelpers/_apis/build/status/AzureStorageTableBackup/AzureStorageTableBackup-CI)](https://applimit.visualstudio.com/CoreHelpers/_build/latest?definitionId=28) |
| CD (push to Docker) | [![Publish Container](https://github.com/CoreHelpers/AzureStorageTableBackup/actions/workflows/publish.yml/badge.svg?branch=master&event=label)](https://github.com/CoreHelpers/AzureStorageTableBackup/actions/workflows/publish.yml) |
| DockerHub | https://hub.docker.com/r/corehelpers/azurebackup/ |
| Based On | https://github.com/CoreHelpers/AzureStorageTable |

# Execute Table Backup with the pre-compiled container

```
docker run \
-e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
-e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_NAME={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \
corehelpers/azurebackup
```

# Execute Table Restore with the pre-compiled container
```
docker run \
-e MODE=Restore \
-e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
-e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e SRC_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \
-e SRC_BACKUP_ID={{YOUR BACKUP ID}} \
-e TGT_ACCOUNT_NAME={{YOUR ACCOUNT NAME}}  \
-e TGT_ACCOUNT_KEY={{YOUE ACCOUNT KEY}} \
corehelpers/azurebackup
```
# Execute Blob Backup with the pre-compiled container
```
docker run \
-e STORAGE_TYPE=Blob \
-e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
-e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_NAME={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \
corehelpers/azurebackup
```

# Execute Blob Restore with the pre-compiled container
```
docker run \
-e MODE=Restore \
-e STORAGE_TYPE=Blob \
-e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
-e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e SRC_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \
-e SRC_BACKUP_ID={{YOUR BACKUP ID}} \
-e TGT_ACCOUNT_NAME={{YOUR ACCOUNT NAME}}  \
-e TGT_ACCOUNT_KEY={{YOUE ACCOUNT KEY}} \
corehelpers/azurebackup
```

# Available Variables

| Variable | Mandatory | Default | Description |
|---|---|---|---|
| MODE | No | Backup | Defines if the container runs in backup or restore mode |
| STORAGE_TYPE | No | Table | Defines if the backup runs for blobs or tables |
| COMPRESS | No | True | Defines if the blob data will be compressed before uploading again (table data will always be compressed). If False, server side copy is used. |
| TRD_COUNT | No | 10 | ParallelOperationThreadCount: Gets or sets the number of blocks that may be simultaneously uploaded (blobs only).|
| UPL_THRESHOLD | No | 32 MiB | SingleBlobUploadThresholdInBytes: Gets or sets the maximum size of a blob in bytes that may be uploaded as a single blob. Ignored if TRD_COUNT greater than 1 (blobs only).|
| SRC_ACCOUNT_NAME | Yes | n/a | Defines the account where the system should backup or restore from |
| SRC_ACCOUNT_KEY | Yes | n/a | Defines the storage account key |
| SRC_ACCOUNT_CONTAINER | Yes when restore | n/a | Defines the container name the restore should load from |
| SRC_BACKUP_ID | Yes when restore | n/a | Defines the backup id which is the file prefix generated during backup which is used for restore |
| SRC_ACCOUNT_ENDPOINT_SUFFIX | No | empty | Allows to send endpoint suffixes for special Azure regions |
| SRC_EXCLUDE_TABLES | No | empty | Allows to define tables which are excluded from backup |
| SRC_EXCLUDE_BLOB_CONTAINER | No | empty | Allows to define blob containers which are excluded from backup |
| TGT_ACCOUNT_NAME | Yes | n/a | Defines the account where the system should backup or restore to |
| TGT_ACCOUNT_KEY | Yes | n/a | Defines the storage account key |
| TGT_ACCOUNT_CONTAINER | Yes when backup | n/a | Defines the container name the backup should store to |
| TGT_ACCOUNT_ENDPOINT_SUFFIX | No | empty | Allows to send endpoint suffixes for special Azure regiosn |

# Build the backup container manually

Step 1: publish the runner into a dedicated directory
```
$: dotnet publish ./backup.runner/ -c Release -o ../publish/runner
```

Step 2: build the docker container
```
$: docker build -t corehelpers/azurebackup:manual -f ./Dockerfile ./publish/runner
```
