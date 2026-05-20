using ForestChurches.Components.AutoEvents;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ForestChurches.Pages.Admin.Events
{
    [Authorize(Policy = "EventManagement.Edit")]
    public class EditModel : PageModel
    {
        private readonly ILogger<EditModel> _logger;
        private ForestChurchesContext _context;
        private readonly ImageInterface _ImageRepository;
        private readonly EventInterface _eventController;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;
        private readonly UserManager<ChurchAccount> _userManager;

        public EventsModel currentEvent { get; set; }
        public List<SelectListItem> UserList { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public EventsModel Input { get; set; }

        [BindProperty]
        public string SelectedUsername { get; set; }

        public EditModel(ILogger<EditModel> logger, 
            ForestChurchesContext context, 
            ImageInterface imageRepository, 
            EventInterface eventController, 
            UserManager<ChurchAccount> userManager, 
            Components.Security.Encoder Encoder, 
            ForestChurches.Components.Storage.IBlobUrlService blobUrlService)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
            _blobUrlService = blobUrlService;
            _ImageRepository = imageRepository;
            _eventController = eventController;
            _Encoder = Encoder;
        }

        public void OnGet(string id)
        {
            try
            {
                currentEvent = _context.Events.Where(x => x.ID == _Encoder.Unprotect<string>(id, "fc.data.admin.events")).FirstOrDefault();

                Input = new EventsModel
                {
                    Name = currentEvent.Name ?? string.Empty,
                    Description = currentEvent.Description ?? string.Empty,
                    Date = currentEvent.Date != default ? currentEvent.Date : DateOnly.FromDateTime(DateTime.Now.Date),
                    User = currentEvent.User ?? string.Empty,
                    Address = currentEvent.Address ?? string.Empty,
                    StartTime = currentEvent.StartTime != default ? currentEvent.StartTime : TimeOnly.MinValue,
                    EndTime = currentEvent.EndTime != default ? currentEvent.StartTime : TimeOnly.MaxValue,
                    Church = currentEvent.Church ?? string.Empty,
                    Link = currentEvent.Link ?? string.Empty,
                    Repeats = currentEvent.Repeats ?? string.Empty,
                    SelectedUsername = currentEvent.User
                };

                var users = _context.Users.ToList();

                UserList = users.Select(user => new SelectListItem{
                    Text = user.UserName,
                    Value = user.UserName,
                    Selected = user.UserName == currentEvent?.User
                }).ToList();


                // Set SelectedUsername to the current event's user, if available
                SelectedUsername = currentEvent?.User;
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUpdateEvent(string id)
        {
            byte[] processedImage = new byte[5];
            try
            {
                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    StatusMessage = "Invalid input, please try again...";
                    return Page();
                }

                // Decode the selected username
                var decodedUsername = _Encoder.Unprotect<string>(SelectedUsername, "fc.data.admin.events");
                var user = await _userManager.FindByEmailAsync(decodedUsername);

                if (Input.Image != null)
                {
                    processedImage = await _ImageRepository.ConvertToByteArray(Input.Image);
                }
                else
                {
                    // IMGUR-1
                    var sasUrl = _blobUrlService.GetReadSasUrl("IMGUR-1.png", TimeSpan.FromDays(1));
                    processedImage = await _ImageRepository.ConvertToByteArrayFromUrl(sasUrl); ;
                }

                Input.ID = _Encoder.Unprotect<string>(id, "fc.data.admin.events");
                await _eventController.UpdateEventAsync(user, Input, processedImage).ConfigureAwait(true);

                StatusMessage = $"Event ({Input.Name}) successfully updated...";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
            }

            return RedirectToPage("./Index");
        }

        private void LogModelErrors()
        {
            foreach (var error in ModelState.Values.SelectMany(v => v.Errors))
            {
                _logger.LogError(error.ErrorMessage);
            }
        }

    }
}
