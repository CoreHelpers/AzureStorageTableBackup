# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:7.0 as build-env
WORKDIR /app

# Copy everything
COPY . ./

# Restore as distinct layers
RUN dotnet restore

# Build and publish a release
RUN dotnet publish backup.runner/backup.runner.csproj -c Release -o out /p:UseAppHost=false

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:7.0

WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "backup.runner.dll"]
