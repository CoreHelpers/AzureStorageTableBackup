# Status

| Build | Status | 
|---|---|
| CI (last changed branch) |[![Continuous Integration](https://github.com/CoreHelpers/AzureStorageTableBackup/actions/workflows/ci.yml/badge.svg)](https://github.com/CoreHelpers/AzureStorageTableBackup/actions/workflows/ci.yml) |
| CD (push to Docker) | [![Publish Container](https://github.com/CoreHelpers/AzureStorageTableBackup/actions/workflows/publish.yml/badge.svg?branch=master&event=label)](https://github.com/CoreHelpers/AzureStorageTableBackup/actions/workflows/publish.yml) |
| DockerHub | https://hub.docker.com/r/corehelpers/azurebackup/ |
| Based On | https://github.com/CoreHelpers/AzureStorageTable |

# Execute Table Backup with the pre-compiled container
The engine is manifest based so build a new manifest with the following structure and launch the docker container. 
Multiple backups can be defined in one manifest file.

```json
{
    "Id": "<<uuid>>",
    "Items": [
        {
            "Id": "<<uuid>>",
            "Name": "<<displayname>>",
            "Enabled": true,

            "Operation": "backup",
            "StorageType": "table",

            "TargetConnectionString": "<<ConnectionString where the backup should be stored>>",
            "TargetContainer": "<<Container the backup should be stored>>",            

            "SourceConnectionString": "<<ConnectionString where the data should be read from>>",

            "Excludes": [
                "<<Regex for tables to exclude>>"
            ],

            "FinishedHook": "<<Optional Finish Hook for monitoring>>"
        }
    ]
}
```

The docker container can be launched as follows:
```
docker run \
-e MANIFEST={{FILE or URL to the manifest}}
corehelpers/azurebackup:<<version>>
```

# Execute Table Restore with the pre-compiled container
The engine is manifest based so build a new manifest with the following structure and launch the docker container.
Multiple backups can be defined in one manifest file.

```json
{
    "Id": "<<uuid>>",
    "Items": [
        {
            "Id": "<<uuid>>",
            "Name": "<<displayname>>",
            "Enabled": true,

            "Operation": "restore",
            "StorageType": "table",

            "SourceConnectionString": "<<ConnectionString where the backup is stored>>",
            "SourceContainer": "<<Container the backup is stored>>",            

            "TargetConnectionString": "<<ConnectionString where the data should be restored to>>",
        }
    ]
}
```

The docker container can be launched as follows:
```
docker run \
-e MANIFEST={{FILE or URL to the manifest}}
corehelpers/azurebackup:<<version>>
```

# Build the backup container manually

Step 1: publish the runner into a dedicated directory
```
$: dotnet publish ./backup.runner/ -c Release -o ../publish/runner
```

Step 2: build the docker container
```
$: docker build -t corehelpers/azurebackup:manual -f ./Dockerfile ./publish/runner
```
