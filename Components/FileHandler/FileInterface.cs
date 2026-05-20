using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;

namespace ForestChurches.Components.FileManager
{
    public interface FileInterface
    {
        Task<IActionResult> CreateFile(ChurchInformation content, string contentType, string filename = "");
    }
}
