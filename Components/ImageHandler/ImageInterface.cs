using Microsoft.AspNetCore.Http;

namespace ForestChurches.Components.ImageHandler
{
    public interface ImageInterface
    {
        Task<byte[]> ConvertToByteArray(IFormFile file);
        Task<byte[]> ConvertToByteArrayFromUrl(string url);
    }
}
