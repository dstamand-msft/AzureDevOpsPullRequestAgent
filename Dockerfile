# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore as a distinct layer for caching
COPY src/ADOPullRequestAgent/ADOPullRequestAgent.csproj src/ADOPullRequestAgent/
RUN dotnet restore src/ADOPullRequestAgent/ADOPullRequestAgent.csproj

# Copy remaining source and publish
COPY src/ src/
RUN dotnet publish src/ADOPullRequestAgent/ADOPullRequestAgent.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Run as non-root user for security
USER $APP_UID

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ADOPullRequestAgent.dll"]
