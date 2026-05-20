// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System.ComponentModel.DataAnnotations;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly SignInManager<ChurchAccount> _signInManager;

        private readonly ForestChurchesContext _context;

        internal ChurchAccount Currentuser { get; set; }

        [TempData]
        internal Models.ChurchInformation CurrentChurch { get; set; }

        public IndexModel(
            UserManager<ChurchAccount> userManager,
            SignInManager<ChurchAccount> signInManager, 
            ForestChurchesContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>

        [TempData]
        public string statusMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Phone]
            [Display(Name = "Phone number")]
            public string PhoneNumber { get; set; }
        }

        public byte[] ImageArray { get; set; }

        private async Task LoadAsync(ChurchAccount user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Assign logged-in user to 'currentUser' variable && Load the header text
            Currentuser = user;
            CurrentChurch = _context.ChurchInformation.Where(ci => ci.ChurchAccountId == user.Id).FirstOrDefault();

            await LoadAsync(user);
            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (!ModelState.IsValid)
            {
                statusMessage = "Error! We we're unable to save your phone number.";
                await LoadAsync(user);
                return Page();
            }

            else if (user == null)
            {
                return NotFound($"Error! Unable to load User '{_userManager.GetUserId(User)}'.");
            }

            else if (user != null && ModelState.IsValid)
            {
                var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
                if (Input.PhoneNumber != phoneNumber)
                {
                    var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
                    if (!setPhoneResult.Succeeded)
                    {
                        statusMessage = "Error! We we're unable to save your phone number.";
                        return RedirectToPage();
                    }
                }
                await _signInManager.RefreshSignInAsync(user);
            }

            statusMessage = "Success! Your profile was successfully updated";
            return RedirectToPage();
        }
    }
}
