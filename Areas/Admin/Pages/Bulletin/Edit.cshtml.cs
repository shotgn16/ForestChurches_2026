using ForestChurches.Components.Bulletins;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace ForestChurches.Areas.Admin.Pages.Bulletin
{
    [Authorize(Policy = "Admin.Read")]
    public class EditModel : PageModel
    {
        private readonly IBulletinService _service;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public ForestChurches.Models.Bulletin ActiveBulletin { get; set; }

        [BindProperty]
        public IFormFile? PdfUpload { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public EditModel(IBulletinService service, IWebHostEnvironment env)
        {
            _service = service;
            _env = env;
        }

        public async Task OnGetAsync()
        {
            // if id route provided, load that bulletin for edit
            if (RouteData.Values.TryGetValue("id", out var idObj) && int.TryParse(idObj?.ToString(), out var id))
            {
                ActiveBulletin = await _service.GetByIdAsync(id) ?? new ForestChurches.Models.Bulletin { BulletinDate = DateTime.UtcNow };
                return;
            }

            // For creating a new bulletin, present a clean Announcement by default (Title, Description, Links)
            ActiveBulletin = new ForestChurches.Models.Bulletin
            {
                BulletinDate = DateTime.UtcNow,
                Type = "Announcement",
                Title = string.Empty,
                PastoralNote = string.Empty,
                NoticesList = string.Empty
            };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (PdfUpload != null && !string.Equals(Path.GetExtension(PdfUpload.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(PdfUpload), "Please choose a PDF file.");
                return Page();
            }

            if (PdfUpload != null)
            {
                var uploads = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", "bulletins");
                Directory.CreateDirectory(uploads);
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(PdfUpload.FileName);
                var filePath = Path.Combine(uploads, fileName);
                using (var fs = new FileStream(filePath, FileMode.Create))
                {
                    await PdfUpload.CopyToAsync(fs);
                }
                ActiveBulletin.PdfFilename = fileName;
            }

            ActiveBulletin.UpdatedAt = DateTime.UtcNow;
            ActiveBulletin.CreatedAt = ActiveBulletin.CreatedAt == default ? DateTime.UtcNow : ActiveBulletin.CreatedAt;

            await _service.CreateOrUpdateAsync(ActiveBulletin);
            StatusMessage = $"Saved: {ActiveBulletin.Title} was saved.";
            // "/Admin/Bulletin/Index" doesn't exist inside the Admin area, so that redirect threw after saving.
            return RedirectToPage("./Index");
        }
    }
}
