# Stage 1: Base SDK for compiling and restoring dependencies for ARM64 Linux
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy the main project file and restore dependencies specifically for ARM64
COPY *.csproj ./
RUN dotnet restore -r linux-arm64

# Copy the rest of the application code and compile the release binaries
COPY . ./
RUN dotnet publish -c Release -o out -r linux-arm64 --no-restore

# Stage 2: Testing target layer (This is what your Pi 5 runs during validation checks)
FROM build AS test
# If you have a separate unit test project, you would navigate into its folder here.
# Since you have a single unified project structure, we run test on the root:
RUN dotnet test --no-restore

# Stage 3: Clean, optimized production runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

# Expose the port the app runs on
EXPOSE 80

# Run the application
ENTRYPOINT ["dotnet", "ForestChurches.dll"]