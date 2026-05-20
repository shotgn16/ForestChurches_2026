// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ForestChurches.Components.LogReader;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

namespace ForestChurches.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;
        private readonly ForestChurchesContext _context;

        public DownloadPersonalDataModel(
            UserManager<ChurchAccount> userManager,
            ILogger<DownloadPersonalDataModel> logger,
            ForestChurchesContext context)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
        }

        public IActionResult OnGet()
        {
            return NotFound();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            Dictionary<string,string> personalData = new();
            IEnumerable<System.Reflection.PropertyInfo> personalDataProps = null;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            else if (ModelState.IsValid && user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            else if (ModelState.IsValid && user != null)
            {
                _logger.LogInformation($"User with ID '{user.Id}' asked for their personal data.");

                // Only include personal data for download
                personalData = new Dictionary<string, string>();
                personalDataProps = typeof(ChurchAccount).GetProperties().Where(
                                prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await _userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add($"Authenticator Key", await _userManager.GetAuthenticatorKeyAsync(user));

                Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
            }

            return new FileContentResult(JsonSerializer.SerializeToUtf8Bytes(personalData), "application/json");
        }
    }
}
