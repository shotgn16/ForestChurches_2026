using ForestChurches.Components.Bulletins;
using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;

namespace ForestChurches.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class BulletinController : ControllerBase
    {
        private readonly IBulletinService _service;

        public BulletinController(IBulletinService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var bulletin = await _service.GetActiveBulletinAsync();
            if (bulletin == null)
            {
                return Ok(new { status = "success", data = (object?)null });
            }

            string pdfUrl = null;
            if (!string.IsNullOrWhiteSpace(bulletin.PdfFilename))
            {
                pdfUrl = $"{Request.Scheme}://{Request.Host}/uploads/bulletins/{bulletin.PdfFilename}";
            }

            var notices = new List<string>();
            if (!string.IsNullOrWhiteSpace(bulletin.NoticesList))
            {
                try
                {
                    notices = System.Text.Json.JsonSerializer.Deserialize<List<string>>(bulletin.NoticesList) ?? bulletin.NoticesList.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                }
                catch
                {
                    notices = bulletin.NoticesList.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                }
            }

            var hymns = new List<string>();
            if (!string.IsNullOrWhiteSpace(bulletin.HymnsList))
            {
                try
                {
                    hymns = System.Text.Json.JsonSerializer.Deserialize<List<string>>(bulletin.HymnsList) ?? bulletin.HymnsList.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                }
                catch
                {
                    hymns = bulletin.HymnsList.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
                }
            }

            var data = new
            {
                title = bulletin.Title,
                parishName = bulletin.ParishName,
                dateString = bulletin.BulletinDate.ToString("dddd d MMMM yyyy"),
                sundayName = bulletin.SundayName,
                collect = bulletin.CollectPrayer,
                firstReading = bulletin.FirstReading,
                gospelReading = bulletin.GospelReading,
                notices = notices,
                hymns = hymns,
                pastoralNote = bulletin.PastoralNote,
                pdfUrl = pdfUrl
            };

            return Ok(new { status = "success", data });
        }
    }
}
