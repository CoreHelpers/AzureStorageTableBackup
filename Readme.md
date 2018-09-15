# Status

| Build | Status | 
|---|---|
| CI (last changed branch) |[![Build Status](https://applimit.visualstudio.com/CoreHelpers/_apis/build/status/AzureStorageTableBackup/AzureStorageTableBackup-CI)](https://applimit.visualstudio.com/CoreHelpers/_build/latest?definitionId=28) |
| CD (push to Docker) | [![Build Status](https://applimit.visualstudio.com/CoreHelpers/_apis/build/status/AzureStorageTableBackup/AzureStorageTableBackup-CD)](https://applimit.visualstudio.com/CoreHelpers/_build/latest?definitionId=29) |
| DockerHub | https://hub.docker.com/r/corehelpers/azurebackup/ |

# Execute Backup with the pre-compiled container

```
docker run \
-e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
-e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_NAME={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
-e TGT_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \
corehelpers/azurebackup
```

# Execute Restore with the pre-compiled container
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

# Available Variables

| Variable | Mandatory | Default | Description |
|---|---|---|---|
| MODE | No | Backup | Defines if the container runs in backup or restore mode |
| SRC_ACCOUNT_NAME | Yes | n/a | Defines the account where the system should backup or restore from |
| SRC_ACCOUNT_KEY | Yes | n/a | Defines the storage account key |
| SRC_ACCOUNT_CONTAINER | Yes when restore | n/a | Defines the container name the restore should load from |
| SRC_BACKUP_ID | Yes when restore | n/a | Defines the backup id which is the file prefix generated during backup which is used for restore |
| SRC_ACCOUNT_ENDPOINT_SUFFIX | No | empty | Allows to send endpoint suffixes for special Azure regiosn |
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
