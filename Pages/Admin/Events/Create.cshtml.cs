using ForestChurches.Components.Email;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Pages.Admin.Events
{
    [Authorize(Policy = "EventManagement.Write")]
    public class CreateModel : PageModel
    {
        private readonly ILogger<CreateModel> _logger;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurchesContext _context;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;
        private iMailSender _mailSender;
        private ImageInterface _iImage;

        [BindProperty]
        public int RepetitionFrequency { get; set; }

        [BindProperty]
        public EventsModel Input { get; set; }
        public IList<ChurchRoles> Roles;

        [BindProperty]
        public string SelectedUsername { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public List<SelectListItem> UserList { get; set; }
        public string EventID { get; set; }

        public CreateModel(
            ILogger<CreateModel> logger, 
            ImageInterface iImage, 
            UserManager<ChurchAccount> userManager, 
            ForestChurchesContext context, 
            iMailSender mailSender,
            ForestChurches.Components.Storage.IBlobUrlService blobUrlService,
            ForestChurches.Components.Security.Encoder Encoder)
        {
            _logger = logger;
            _iImage = iImage;
            _userManager = userManager;
            _blobUrlService = blobUrlService;
            _context = context;
            _Encoder = Encoder;

            // One option per church, "Church (account)", so accounts that manage several churches
            // (benefices) are unambiguous. The value is "username|churchId".
            var users = _userManager.Users.ToList();
            var churches = _context.ChurchInformation.OrderBy(c => c.Name).ToList();
            UserList = users
                .SelectMany(user => churches.Where(c => c.ChurchAccountId == user.Id).DefaultIfEmpty()
                    .Select(c => new SelectListItem
                    {
                        Text = c == null ? user.UserName : $"{c.Name} ({user.UserName})",
                        Value = c == null ? user.UserName : $"{user.UserName}|{c.ID}"
                    }))
                .OrderBy(i => i.Text)
                .ToList();

            Input = new EventsModel();
        }

        public void OnGet(string eventName)
        {
            Input.Name = eventName;
        }

        [ValidateAntiForgeryToken]
        public async Task<ActionResult> OnPostCreateEvent()
        {
            try
            {
                ModelState.Remove(nameof(Input.SelectedUsername));
                ModelState.Remove(nameof(SelectedUsername));
                ModelState.Remove(nameof(Input.Church));
                ModelState.Remove(nameof(Input.User));

                if (!ModelState.IsValid)
                {
                    LogModelErrors();
                    StatusMessage = "Invalid input, please try again...";
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    _logger.LogInformation("ModelState is valid.");

                    var decodedOwner = _Encoder.Unprotect<string>(SelectedUsername, "fc.data.admin.events");
                    var ownerParts = decodedOwner.Split('|', 2);
                    var decodedUsername = ownerParts[0];
                    var chosenChurchId = ownerParts.Length > 1 ? ownerParts[1] : null;
                    _logger.LogInformation($"Decoded username: {decodedUsername}");

                    var user = await _userManager.FindByEmailAsync(decodedUsername);
                    if (user == null)
                    {
                        _logger.LogWarning($"User with email {decodedUsername} not found.");
                        StatusMessage = "User not found.";
                        return Page();
                    }

                    var church = (chosenChurchId == null ? null : _context.ChurchInformation.FirstOrDefault(i => i.ID == chosenChurchId && i.ChurchAccountId == user.Id))
                                 ?? _context.ChurchInformation.Where(i => i.ChurchAccountId == user.Id).OrderBy(i => i.Name).FirstOrDefault();
                    if (church == null)
                    {
                        _logger.LogWarning($"Church information for user {user.UserName} not found.");
                        StatusMessage = "Church information not found.";
                        return Page();
                    }

                    if (Input != null)
                    {
                        byte[] Image;
                        if (Input.Image != null)
                        {
                            Image = await _iImage.ConvertToByteArray(Input.Image);
                            _logger.LogInformation("Image converted to byte array.");
                        }
                        else
                        {
                            // IMGUR-2
                            var sasUrl = _blobUrlService.GetReadSasUrl("IMGUR-2.png", TimeSpan.FromDays(1));
                            Image = await _iImage.ConvertToByteArrayFromUrl(sasUrl);
                            _logger.LogInformation("Default image used.");
                        }

                        var newEvent = new EventsModel
                        {
                            Name = Input.Name,
                            Description = Input.Description,
                            Address = Input.Address,
                            Date = Input.Date,
                            Repeats = Input.Repeats,
                            StartTime = Input.StartTime,
                            EndTime = Input.EndTime,
                            Link = Input.Link ?? "Not Available",
                            ImageArray = Image,
                            User = user.UserName,
                            Church = church.Name
                        };

                        _context.Events.Add(newEvent);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Event saved to database.");

                        EventID = newEvent.ID;
                        StatusMessage = !string.IsNullOrEmpty(EventID) ? "New Event Successfully Created!" : "An Error occurred while submitting your event.";

                        _mailSender.SendEmailEventCreated(user.Email, "/Events", Input.Repeats, Input);
                        _logger.LogInformation("Email sent to user.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, ex);
                StatusMessage = "Error: " + ex.Message;
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
