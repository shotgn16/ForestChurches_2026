using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Events
{
    [Authorize(Policy = "UserEvents.View")]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private UserManager<ChurchAccount> _userManager;
        private ForestChurchesContext _context;
        private readonly iEmail _mailRepository;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly iMailSender _mailSender;

        internal List<EventsModel> Events;
        internal ChurchAccount CurrentUser { get; set; }

        public IndexModel(
            UserManager<ChurchAccount> userManager, 
            ForestChurchesContext context, 
            ILogger<IndexModel> logger, 
            iEmail mailRepository, 
            iMailSender mailSender,
            ForestChurches.Components.Security.Encoder Encoder)
        {
            _mailRepository = mailRepository;
            _userManager = userManager;
            _mailSender = mailSender;
            _context = context;
            _Encoder = Encoder;
            _logger = logger;
        }

        public void OnGet()
        {
            LoadAsync();
        }

        private async Task<IActionResult> LoadAsync()
        {
            CurrentUser = await _userManager.GetUserAsync(User);
            Events = _context.Events.Where(a => a.User == CurrentUser.Email).ToList();

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostDeleteEvent(string eventId)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    // Fix this when you get back  Currently no ID in the 'value'
                    var eventToDelete = _context.Events.Where(a => a.ID == _Encoder.Unprotect<string>(eventId, "fc.data.user.events")).FirstOrDefault();
                    var eventDeleted = _context.Events.Remove(eventToDelete);

                    _context.SaveChangesAsync();

                    if (eventDeleted.State == Microsoft.EntityFrameworkCore.EntityState.Deleted)
                    {
                        _logger.LogInformation($"Event ({eventToDelete.Name}) successfully deleted");

                        _mailSender.SendEmailEventDeleted(_userManager.GetUserName(User), _userManager.GetUserName(User), "/Events", eventToDelete);
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw ex;
            }

            return RedirectToPage();
        }
    }
}
