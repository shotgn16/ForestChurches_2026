using System.ComponentModel.DataAnnotations;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.Admin.Church
{
    /// <summary>
    /// Site admins add a church to an existing account (e.g. a benefice account that manages several churches).
    /// Church administrators can't add churches themselves.
    /// </summary>
    [Authorize(Policy = "Admin.Read")]
    public class CreateModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly ILogger<CreateModel> _logger;
        private readonly Components.Security.Encoder _encoder;

        public CreateModel(ForestChurchesContext context, ILogger<CreateModel> logger, Components.Security.Encoder encoder)
        {
            _context = context;
            _logger = logger;
            _encoder = encoder;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Choose the account that will manage this church.")]
            public string? AccountId { get; set; }

            [Required(ErrorMessage = "Enter the church's name.")]
            [StringLength(30, ErrorMessage = "Church names can't be longer than 30 characters.")]
            public string? Name { get; set; }

            [Required(ErrorMessage = "Enter the church's address.")]
            public string? Address { get; set; }

            /// <summary>Existing benefice ID, "" for none.</summary>
            public string? BeneficeId { get; set; }
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public List<ChurchAccount> Accounts { get; private set; } = new();
        public List<Benefice> Benefices { get; private set; } = new();
        public Dictionary<string, int> ChurchCounts { get; private set; } = new();

        [TempData]
        public string? StatusMessage { get; set; }

        private async Task LoadListsAsync()
        {
            Accounts = await _context.Users.OrderBy(u => u.Email).ToListAsync();
            Benefices = await _context.Benefices.OrderBy(b => b.Name).ToListAsync();
            ChurchCounts = await _context.ChurchInformation.GroupBy(c => c.ChurchAccountId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        public async Task OnGetAsync(string? account = null, string? benefice = null)
        {
            await LoadListsAsync();
            Input.AccountId = account;
            Input.BeneficeId = benefice;
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostAsync()
        {
            await LoadListsAsync();
            if (!ModelState.IsValid) return Page();

            var account = await _context.Users.FirstOrDefaultAsync(u => u.Id == Input.AccountId);
            if (account == null)
            {
                ModelState.AddModelError("Input.AccountId", "That account no longer exists.");
                return Page();
            }

            string? beneficeId = null;
            if (!string.IsNullOrEmpty(Input.BeneficeId))
            {
                beneficeId = await _context.Benefices.Where(b => b.Id == Input.BeneficeId).Select(b => b.Id).FirstOrDefaultAsync();
                if (beneficeId == null)
                {
                    ModelState.AddModelError("Input.BeneficeId", "That benefice no longer exists.");
                    return Page();
                }
            }

            // New churches start Closed (hidden from the public) until their details are filled in.
            var church = new ChurchInformation
            {
                ChurchAccountId = account.Id,
                Name = Input.Name!.Trim(),
                Address = Input.Address!.Trim(),
                BeneficeId = beneficeId,
                Status = "Closed",
                Description = "Please add your church description here.",
                Denomination = "Not set yet",
                Congregation = "0-10",
                Phone = account.PhoneNumber ?? string.Empty,
            };
            _context.ChurchInformation.Add(church);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Site admin {Admin} added church {ChurchId} to account {AccountId}", User.Identity?.Name, church.ID, account.Id);
            StatusMessage = $"Added {church.Name} to {account.Email}. It's Closed (hidden) until you or the church open it.";
            return RedirectToPage("./Edit", new { id = _encoder.Protect(church.ID, "fc.data.admin.church") });
        }
    }
}
