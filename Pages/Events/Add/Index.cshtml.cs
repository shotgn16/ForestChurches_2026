using ForestChurches.Components.Email;
using ForestChurches.Components.Http;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages.Events.Add
{
    [Authorize(Policy = "UserEvents.Add")]
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private ForestChurchesContext _context;
        private ImageInterface _iImage;
        private UserManager<ChurchAccount> _userManager;
        private readonly iMailSender _mailSender;
        private readonly ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;
        private readonly ForestChurches.Components.Storage.IBlobStorageService _blobStorageService;


        // Binds form input to this property
        [BindProperty]
        public EventsModel Input { get; set; }

        internal ChurchAccount CurrentUser;
        internal ChurchInformation CurrentChurch;

        [TempData]
        public string StatusMessage { get; set; }

        public IndexModel(ILogger<IndexModel> logger, IHttpWrapper httpWrapper, ForestChurchesContext context,
            ImageInterface iImage, iMailSender mailSender, UserManager<ChurchAccount> userManager, ForestChurches.Components.Storage.IBlobUrlService blobUrlService, ForestChurches.Components.Storage.IBlobStorageService blobStorageService)
        {
            _logger = logger;
            _context = context;
            _iImage = iImage;
            _userManager = userManager;
            _blobUrlService = blobUrlService;
            _blobStorageService = blobStorageService;
            _mailSender = mailSender;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (Input == null)
            {
                Input = new EventsModel();
            }
            await LoadAsync().ConfigureAwait(true);
            return Page();
        }

        private async Task LoadAsync()
        {
            System.Security.Principal.IIdentity user = HttpContext.User.Identity ?? null;
            if (user != null)
            {
                CurrentUser = await _userManager.FindByNameAsync(user.Name);
            }
            CurrentChurch = _context.ChurchInformation.FirstOrDefault(a => a.ChurchAccountId == CurrentUser.Id);
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            // Create a variable and assign a random value to it. 
            byte[] processedImage = new byte[1];

            CurrentUser = await _userManager.GetUserAsync(User);
            CurrentChurch = _context.ChurchInformation.Where(a => a.ChurchAccountId == CurrentUser.Id).FirstOrDefault();

            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors);
                    foreach (var error in errors)
                    {
                        Console.WriteLine(error.ErrorMessage);
                    }
                    return Page();
                }

                else if (ModelState.IsValid)
                {
                    string imageBlobName = null;
                    // Convert / upload the image
                    if (Input.Image == null)
                    {
                        // Use default image stored in blob storage - reference the existing blob name
                        imageBlobName = "IMGUR-1.png";
                        processedImage = await _iImage.ConvertToByteArrayFromUrl(_blobUrlService.GetReadSasUrl(imageBlobName, TimeSpan.FromDays(1)));
                    }
                    else
                    {
                        // Upload to images container and store returned blob name
                        imageBlobName = await _blobStorageService.UploadAsync(Input.Image);
                        processedImage = await _iImage.ConvertToByteArray(Input.Image);
                    }

                    // Add the rest of the information to the event...
                    var newEvent = new EventsModel()
                    {
                        ID = Guid.NewGuid().ToString(),
                        Name = Input.Name,
                        Description = Input.Description,
                        Date = Input.Date,
                        User = CurrentUser.UserName,
                        Address = Input.Address,
                        StartTime = Input.StartTime,
                        EndTime = Input.EndTime,
                        Church = CurrentChurch.Name,
                        ImageArray = processedImage,
                        ImageBlobName = imageBlobName,
                        Repeats = Input.Repeats,
                        Link = Input.Link ?? "Not Available"
                    };

                    await _context.Events.AddAsync(newEvent);
                    await _context.SaveChangesAsync();

                    StatusMessage = $"Event ({Input.Name}) successfully created!";

                    if (StatusMessage == $"Event ({Input.Name}) successfully created!")
                    {
                        _mailSender.SendEmailEventCreated(CurrentUser.Email, "/Events", Input.Repeats, Input);
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());

                StatusMessage = $"An error occurred while creating the event ({Input.Name}).";

                throw ex;

            }

            return Page();
        }
    }
}
