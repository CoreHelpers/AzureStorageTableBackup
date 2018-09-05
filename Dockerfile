# Take the right base image
FROM microsoft/aspnetcore:2.0

# Replace shell with bash so we can source files
RUN rm /bin/sh && ln -s /bin/bash /bin/sh

# install the build essentials
RUN apt-get update && apt-get install -y build-essential libssl-dev curl procps \
  && apt-get -y autoclean

# Create app directory
WORKDIR /usr/src/app.backup
