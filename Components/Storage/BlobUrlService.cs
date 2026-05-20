using System;
using System.IO;
using System.Linq;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace ForestChurches.Components.Storage
{
    public class BlobUrlService : IBlobUrlService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _accountName;
        private readonly string _accountKey;
        private readonly string _containerName;

        public BlobUrlService(string connectionString, string accountName, string accountKey, string containerName)
        {
            _blobServiceClient = new BlobServiceClient(connectionString);
            _accountName = accountName;
            _accountKey = accountKey;
            _containerName = containerName;
        }

        public string GetReadSasUrl(string blobName, System.TimeSpan validFor)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            // Try the exact name first
            var blobClient = containerClient.GetBlobClient(blobName);

            bool exists = false;
            try
            {
                exists = blobClient.ExistsAsync().GetAwaiter().GetResult();
            }
            catch
            {
                exists = false;
            }

            // If exact blob not found, attempt to locate a blob in the container whose filename
            // matches the provided blobName (handles virtual folder prefixes like "images/IMGUR-1.png").
            if (!exists)
            {
                try
                {
                    var match = containerClient.GetBlobs().FirstOrDefault(b =>
                        string.Equals(Path.GetFileName(b.Name), blobName, StringComparison.OrdinalIgnoreCase) ||
                        b.Name.EndsWith(blobName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (match != null)
                    {
                        blobName = match.Name; // use the actual stored name
                        blobClient = containerClient.GetBlobClient(blobName);
                        exists = true;
                    }
                }
                catch
                {
                    // ignore listing errors and fall back to returning empty
                    exists = false;
                }
            }

            if (!exists)
            {
                // Blob not found — return empty so callers can provide a fallback
                return string.Empty;
            }

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(validFor)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var credential = new StorageSharedKeyCredential(_accountName, _accountKey);
            var sas = sasBuilder.ToSasQueryParameters(credential).ToString();

            return $"{blobClient.Uri}?{sas}";
        }
    }
}
