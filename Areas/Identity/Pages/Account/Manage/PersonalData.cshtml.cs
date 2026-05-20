// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Areas.Identity.Pages.Account.Manage
{
    public class PersonalDataModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ILogger<PersonalDataModel> _logger;
        private readonly ForestChurchesContext _context;

        internal ChurchAccount CurrentUser { get; set; }
        internal Models.ChurchInformation CurrentChurch { get; set; }

        public PersonalDataModel(
            UserManager<ChurchAccount> userManager,
            ILogger<PersonalDataModel> logger, 
            ForestChurchesContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        public async Task<IActionResult> OnGet()
        {
            var user = await _userManager.GetUserAsync(User);
            
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            CurrentUser = user;
            CurrentChurch = _context.ChurchInformation.Where(ci => ci.ChurchAccount.Id == CurrentUser.Id).FirstOrDefault();

            return Page();
        }
    }
}
