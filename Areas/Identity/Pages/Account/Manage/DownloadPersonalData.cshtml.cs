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
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF;
using QuestPDF.Helpers;

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
                Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.pdf");
            }

            // Generate PDF using QuestPDF (inline document builder)
            byte[] pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.PageColor(Colors.White);

                    page.Header().Height(50).Row(row =>
                    {
                        row.RelativeColumn().Stack(stack =>
                        {
                            stack.Item().Text("Personal Data Export").FontSize(20).Bold();
                            stack.Item().Text($"User: {User?.Identity?.Name ?? _userManager.GetUserId(User)}").FontSize(12).SemiBold();
                        });
                        row.ConstantColumn(100).AlignRight().Text(text => text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm UTC")).FontSize(10));
                    });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        column.Spacing(5);

                        foreach (var kv in personalData)
                        {
                            column.Item().Row(r =>
                            {
                                r.ConstantColumn(250).Text(kv.Key).FontSize(11).Bold();
                                r.RelativeColumn().Text(kv.Value).FontSize(11);
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x => x.Span("ForestChurches - Personal Data Export").FontSize(9));
                });
            }).GeneratePdf();

            return new FileContentResult(pdfBytes, "application/pdf");
        }
    }
}
