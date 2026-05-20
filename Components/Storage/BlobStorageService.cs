using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace ForestChurches.Components.Storage
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _client;
        private readonly string _defaultContainer;

        public BlobStorageService(string connectionString, string defaultContainer)
        {
            _client = new BlobServiceClient(connectionString);
            _defaultContainer = defaultContainer;
        }
    
        private BlobContainerClient GetContainer(string? containerName)
        {
            var name = string.IsNullOrEmpty(containerName) ? _defaultContainer : containerName;
            var container = _client.GetBlobContainerClient(name);
            container.CreateIfNotExists(PublicAccessType.None);
            return container;
        }

        public async Task<string> UploadAsync(IFormFile file, string containerOverride = null, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(file.FileName);
            var blobName = $"images/{Guid.NewGuid()}{ext}";
            var container = GetContainer(containerOverride);
            var blob = container.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders
            {
                ContentType = file.ContentType
            };

            await using var stream = file.OpenReadStream();
            await blob.UploadAsync(stream, headers, cancellationToken: ct);
            return blobName;
        }

        public async Task<string> UploadAsync(byte[] data, string fileName, string contentType, string containerOverride = null, CancellationToken ct = default)
        {
            var container = GetContainer(containerOverride);
            var blob = container.GetBlobClient(fileName);
            using var stream = new MemoryStream(data);
            var headers = new BlobHttpHeaders { ContentType = contentType };
            await blob.UploadAsync(stream, headers, cancellationToken: ct);
            return fileName;
        }

        public async Task DeleteAsync(string blobName, string containerOverride = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(blobName)) return;
            var container = GetContainer(containerOverride);
            var blob = container.GetBlobClient(blobName);
            await blob.DeleteIfExistsAsync(cancellationToken: ct);
        }
    }
}
