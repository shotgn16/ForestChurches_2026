# Multi-stage Dockerfile for ForestChurches targeting .NET 8 (suitable for Azure container hosting)

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore as separate layers for better caching
COPY *.csproj ./
RUN dotnet restore

# Copy everything and publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Tell .NET 8 to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

# Expose port 8080
EXPOSE 8080

# Start the app
ENTRYPOINT ["dotnet", "ForestChurches.dll"]