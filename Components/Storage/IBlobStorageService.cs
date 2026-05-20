using Microsoft.AspNetCore.Http;

namespace ForestChurches.Components.Storage
{
    public interface IBlobStorageService
    {
        Task<string> UploadAsync(IFormFile file, string containerOverride = null, CancellationToken ct = default);
        Task<string> UploadAsync(byte[] data, string fileName, string contentType, string containerOverride = null, CancellationToken ct = default);
        Task DeleteAsync(string blobName, string containerOverride = null, CancellationToken ct = default);
    }
}
