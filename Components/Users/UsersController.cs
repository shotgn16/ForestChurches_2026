using ForestChurches.Components.Configuration;
using ForestChurches.Components.ImageHandler;
using ForestChurches.Components.Roles;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Net;

public class UsersController : Controller, UserInterface
{
	private readonly ForestChurchesContext _context;
	private readonly Configuration _configuration;
	private readonly ILogger<UsersController> _logger;
    private readonly ForestChurches.Components.Storage.IBlobUrlService _blobUrlService;

    public UsersController(ForestChurchesContext context, Configuration configuration, ILogger<UsersController> logger, ForestChurches.Components.Storage.IBlobUrlService blobUrlService)
	{
		_context = context;
		_configuration = configuration;
        _blobUrlService = blobUrlService;
        _logger = logger;
	}

	public async Task SeedSuperAdminAsync(UserManager<ChurchAccount> userManager, RoleManager<ChurchRoles> roleManager)
	{
		_logger.LogInformation("Seeding Super Admin...");
        var ChurchInformation2 = new ChurchInformation
		{
			Name = "Default Church",
			Denomination = "Anglican",
			Website = new Uri ("https://forestchurches.co.uk/"),
			Description = "Some church description. (This is not an actual church.)",
			WheelchairAccess = true,
			Wifi = true,
			Parking = true,
			Childcare = true,
			HearingAidSupport = true,
			Kitchen = false,
			MeetingHall = false,
			Refreshments = true,
			Restrooms = true,
			Address = "33 Walden Road Greenheads, AB42 4NA",
			Churchsuite = new Uri("https://login.churchsuite.com/?account=demonstration"),
			Congregation = "20-50",
			Phone = "0000-0000-000",
			Status = "Open",
			ServiceTimes = new List<ServiceTimes>
			{
				new ServiceTimes { Time = new TimeOnly(9,00).ToString(), Note = "Early Morning Service" },
				new ServiceTimes { Time = new TimeOnly(11,00).ToString(), Note = "Morning Traditional Service" }
			}
		};

        var blobName = "IMGUR-2.png";
        var sasUrl = _blobUrlService.GetReadSasUrl(blobName, TimeSpan.FromDays(1));

        var superAdminUser = new ChurchAccount
        {
            Id = Guid.NewGuid().ToString(),
            Email = _configuration.Client.GetSecret("sys-admin-username").Value.Value,
            UserName = _configuration.Client.GetSecret("sys-admin-username").Value.Value,
            EmailConfirmed = true,
            ChurchInformation = ChurchInformation2,
            CreatedDate = DateTime.UtcNow.ToString(),
            // store blob name only; image bytes are kept in blob storage
            ImageBlobName = blobName,
            // ImageArray column in database is NOT NULL; provide empty array to satisfy constraint
            ImageArray = Array.Empty<byte>(),
            Role = Roles.SuperAdmin.ToString()
        };

		if (userManager.Users.All(u => u.Id != superAdminUser.Id))
		{
			var user = await userManager.FindByEmailAsync(superAdminUser.Email);

			if (user == null)
			{
				_logger.LogInformation("Creating super admin user...");
				await userManager.CreateAsync(superAdminUser, _configuration.Client.GetSecret("sys-admin-password").Value.Value).ConfigureAwait(true);
				await userManager.AddToRoleAsync(superAdminUser, "SuperAdmin").ConfigureAwait(true);

				ChurchInformation2.ChurchAccountId = superAdminUser.Id;

				var existingChurchInfo = _context.ChurchInformation.FirstOrDefault(ci => ci.ChurchAccountId == ChurchInformation2.ChurchAccountId);
				if (existingChurchInfo == null)
				{
					_context.ChurchInformation.Add(ChurchInformation2);
					_context.SaveChanges();
					_logger.LogInformation("Church information added.");
				}
				else
				{
					_logger.LogWarning($"ChurchInformation with ChurchAccountId {ChurchInformation2.ChurchAccountId} already exists.");
				}
			}
			else
			{
				_logger.LogInformation("Super admin user already exists.");
			}
		}
	}

	private async Task<byte[]> ConvertLinkToByteArray(string url)
	{
		using (WebClient client = new WebClient())
		{
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

			byte[] imagebytes = client.DownloadData(url);

			return imagebytes;
		}
	}
}
