# Take the right base image
FROM mcr.microsoft.com/dotnet/core/aspnet:2.2

# Replace shell with bash so we can source files
RUN rm /bin/sh && ln -s /bin/bash /bin/sh

# install the build essentials
RUN apt-get update && apt-get install -y build-essential libssl-dev curl procps \
  && apt-get -y autoclean

# Create app directory
WORKDIR /usr/src/app.backup.runner

# Copy everything
COPY ./ .

# execute the command
CMD [ "dotnet", "backup.runner.dll" ]
