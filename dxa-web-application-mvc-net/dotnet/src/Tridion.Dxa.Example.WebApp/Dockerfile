# Use official ASP.NET Core runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 80
EXPOSE 443

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Add the internal Nexus repository for restoring packages
RUN dotnet nuget add source https://nexus.sdl.com/repository/releases_dotnet/index.json --name "SDL-Nexus"

RUN dotnet nuget locals all --clear

COPY ["Tridion.Dxa.Example.WebApp.csproj", "./"]

RUN dotnet restore Tridion.Dxa.Example.WebApp.csproj \
    --source https://api.nuget.org/v3/index.json \
    --source https://nexus.sdl.com/repository/releases_dotnet/index.json


COPY . .

RUN apt-get update && \
    apt-get install -y --no-install-recommends ca-certificates && \
    dotnet dev-certs https --trust

RUN dotnet publish "Tridion.Dxa.Example.WebApp.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set defaults 
ENV Logging__LogLevel__Default=Warning
    #URLs=http://*:80

ENTRYPOINT ["dotnet", "Tridion.Dxa.Example.WebApp.dll"]
