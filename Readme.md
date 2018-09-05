Build the backup container manually
***********************************

Step 1: publish the runner into a dedicated directory
$: dotnet publish ./backup.runner/ -c Release -o ../publish/runner

Step 2: build the docker container
$: docker build -t corehelpers/azurebackup:manual -f ./Dockerfile ./publish/runner

Step 3: run the manually generated container
docker run \
  -e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
  -e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
  -e TGT_ACCOUNT_NAME={{YOUR ACCOUNT KEY}} \
  -e TGT_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
  -e TGT_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \  
  corehelpers/azurebackup:manual
  
Pull the pre-compiled container
*******************************

Step 1: pull the container from public repository
$: docker pull corehelpers/azurebackup

Step 3: run the container
docker run \
  -e SRC_ACCOUNT_NAME={{YOUR ACCOUNT NAME}} \
  -e SRC_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
  -e TGT_ACCOUNT_NAME={{YOUR ACCOUNT KEY}} \
  -e TGT_ACCOUNT_KEY={{YOUR ACCOUNT KEY}} \
  -e TGT_ACCOUNT_CONTAINER={{YOUR ACCOUNT CONTAINER}} \  
  corehelpers/azurebackup
