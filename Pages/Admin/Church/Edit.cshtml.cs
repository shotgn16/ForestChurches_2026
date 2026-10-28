using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Pages.Admin.Church
{
    // Previously had no [Authorize], so the page and its POST handler were reachable without signing in.
    [Authorize(Policy = "Admin.Read")]
    public class EditModel : PageModel
    {
        private readonly ForestChurchesContext _context;
        private readonly ILogger<EditModel> _logger;
        private readonly UserManager<ChurchAccount> _userManager;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;

        public EditModel(
            ForestChurchesContext context,
            ILogger<EditModel> logger, UserManager<ChurchAccount> userManager, Components.Security.Encoder Encoder)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
            _Encoder = Encoder;
        }

        [BindProperty]
        public ProfileUpdateModel SaveChangesModel { get; set; }

        public ChurchInformation Church { get; private set; }

        [TempData]
        internal string ChurchID { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        // ---- Benefice & management -----------------------------------------------------------

        /// <summary>Encrypted ID of the church on screen (for the benefice/management forms).</summary>
        public string ChurchKey { get; private set; } = string.Empty;
        public List<Benefice> Benefices { get; private set; } = new();
        public List<ChurchAccount> Accounts { get; private set; } = new();
        public ChurchAccount? Owner { get; private set; }
        /// <summary>Other churches managed by the same account.</summary>
        public List<ChurchInformation> AccountChurches { get; private set; } = new();

        private async Task LoadManagementAsync()
        {
            if (Church == null) return;
            ChurchKey = _Encoder.Protect(Church.ID, "fc.data.admin.church");
            Benefices = await _context.Benefices.OrderBy(b => b.Name).ToListAsync();
            Accounts = await _context.Users.OrderBy(u => u.Email).ToListAsync();
            Owner = Accounts.FirstOrDefault(a => a.Id == Church.ChurchAccountId);
            AccountChurches = await _context.ChurchInformation
                .Where(c => c.ChurchAccountId == Church.ChurchAccountId && c.ID != Church.ID)
                .OrderBy(c => c.Name).ToListAsync();
        }

        private async Task<ChurchInformation?> FindByKeyAsync(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            try
            {
                var id = _Encoder.Unprotect<string>(key, "fc.data.admin.church");
                return await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ID == id);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Sets the church's benefice and/or moves it to another account.</summary>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostManageAsync(string id, string? beneficeId, string? accountId)
        {
            var church = await FindByKeyAsync(id);
            if (church == null)
            {
                StatusMessage = "Error: that church no longer exists.";
                return RedirectToPage("./Index");
            }

            var messages = new List<string>();

            // Benefice ("" = none)
            var newBenefice = string.IsNullOrEmpty(beneficeId) ? null
                : await _context.Benefices.Where(b => b.Id == beneficeId).Select(b => b.Id).FirstOrDefaultAsync();
            if (!string.IsNullOrEmpty(beneficeId) && newBenefice == null)
            {
                StatusMessage = "Error: that benefice no longer exists.";
                return RedirectToPage(new { id });
            }
            if (church.BeneficeId != newBenefice)
            {
                church.BeneficeId = newBenefice;
                messages.Add(newBenefice == null ? "removed from its benefice" : "benefice updated");
            }

            // Managing account
            if (!string.IsNullOrEmpty(accountId) && accountId != church.ChurchAccountId)
            {
                var newOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == accountId);
                var oldOwner = await _context.Users.FirstOrDefaultAsync(u => u.Id == church.ChurchAccountId);
                if (newOwner == null)
                {
                    StatusMessage = "Error: that account no longer exists.";
                    return RedirectToPage(new { id });
                }

                // "Held at" links only make sense between churches on the same account.
                var crossLinks = await _context.ServiceTimes
                    .Where(st => (st.ChurchInformationId == church.ID && st.LocationChurchId != null) || st.LocationChurchId == church.ID)
                    .ToListAsync();
                foreach (var st in crossLinks)
                {
                    st.LocationChurchId = null;
                    if (string.IsNullOrWhiteSpace(st.LocationAddress)) st.UseCustomLocation = false;
                }

                // The church's events (owned by the old account) go with it.
                if (oldOwner != null)
                {
                    var owners = new[] { oldOwner.UserName, oldOwner.Email }.Where(o => !string.IsNullOrEmpty(o)).Distinct().ToList();
                    var events = await _context.Events.Where(e => e.Church == church.Name && owners.Contains(e.User)).ToListAsync();
                    foreach (var e in events) e.User = newOwner.UserName ?? newOwner.Email ?? e.User;
                    if (events.Count > 0) messages.Add($"{events.Count} event(s) moved with it");
                }

                church.ChurchAccountId = newOwner.Id;
                messages.Add($"now managed by {newOwner.Email}");
                _logger.LogInformation("Site admin {Admin} moved church {ChurchId} from account {From} to {To}",
                    User.Identity?.Name, church.ID, oldOwner?.Id, newOwner.Id);
            }

            await _context.SaveChangesAsync();
            StatusMessage = messages.Count == 0 ? "No changes." : $"Saved: {church.Name} {string.Join("; ", messages)}.";
            return RedirectToPage(new { id });
        }

        /// <summary>Deletes a church from an account that manages other churches (SuperAdmin only).</summary>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostRemoveChurchAsync(string id)
        {
            if (!User.IsInRole("SuperAdmin"))
            {
                StatusMessage = "Error: only a SuperAdmin can remove a church.";
                return RedirectToPage(new { id });
            }
            var church = await FindByKeyAsync(id);
            if (church == null)
            {
                StatusMessage = "Error: that church no longer exists.";
                return RedirectToPage("./Index");
            }
            var siblings = await _context.ChurchInformation.CountAsync(c => c.ChurchAccountId == church.ChurchAccountId && c.ID != church.ID);
            if (siblings == 0)
            {
                StatusMessage = "Error: this is the account's only church. Close it instead, or delete the account from Users.";
                return RedirectToPage(new { id });
            }

            // Services elsewhere that were held here fall back to their own church.
            var heldHere = await _context.ServiceTimes.Where(st => st.LocationChurchId == church.ID).ToListAsync();
            foreach (var st in heldHere)
            {
                st.LocationChurchId = null;
                if (string.IsNullOrWhiteSpace(st.LocationAddress)) st.UseCustomLocation = false;
            }
            _context.ChurchInformation.Remove(church); // its services and their dates go with it
            await _context.SaveChangesAsync();
            _logger.LogWarning("SuperAdmin {Admin} removed church {ChurchId} ({Name})", User.Identity?.Name, church.ID, church.Name);
            StatusMessage = $"Removed {church.Name}. Its events weren't deleted; remove them from Events if they're no longer needed.";
            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            try
            {
                Church = _context.ChurchInformation.Where(c => c.ID == _Encoder.Unprotect<string>(id, "fc.data.admin.church")).FirstOrDefault();
                ChurchID = _Encoder.Unprotect<string>(id, "fc.data.admin.church");

                if (Church == null)
                {
                    StatusMessage = "Error: that church no longer exists.";
                    return Page();
                }

                SaveChangesModel = new ProfileUpdateModel
                {
                    ID = Church.ID,
                    Name = Church.Name,
                    Description = Church.Description,
                    Denomination = Church.Denomination,
                    Congregation = Church.Congregation,
                    Address = Church.Address,
                    Phone = Church.Phone,
                    Churchsuite = Church.Churchsuite,
                    Website = Church.Website,
                    Parking = Church.Parking,
                    Restrooms = Church.Restrooms,
                    WheelchairAccess = Church.WheelchairAccess,
                    Wifi = Church.Wifi,
                    Refreshments = Church.Refreshments,
                    HearingAidSupport = Church.HearingAidSupport,
                    Kitchen = Church.Kitchen,
                    MeetingHall = Church.MeetingHall,
                    Childcare = Church.Childcare,
                    OutdoorSpaces = Church.OutdoorSpaces,
                    WorshipHall = Church.WorshipHall
                };
                await LoadManagementAsync();
            }
            catch (Exception ex)
            {
                // Don't unprotect again here: an invalid or expired id is the usual cause and would throw a second time.
                _logger.LogError(ex, "An error occurred while loading a church for editing.");
                StatusMessage = "Error: that link is invalid or has expired. Please open the church again from the list.";
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostUpdateChurch()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    StatusMessage = "Error: please check the highlighted fields.";
                    Church = await _context.ChurchInformation.FirstOrDefaultAsync(c => c.ID == SaveChangesModel.ID);
                    return Page();
                }

                if (SaveChangesModel == null)
                {
                    StatusMessage = "Error: Unable to save changes. Please try again later.";
                    return Page();
                }

                // Update the church being edited (its ID is posted with the form). This used to update the
                // signed-in admin's own church instead of the one on screen.
                var church = await _context.ChurchInformation
                    .FirstOrDefaultAsync(c => c.ID == SaveChangesModel.ID);
                if (church == null)
                {
                    StatusMessage = "Error: that church no longer exists.";
                    return RedirectToPage("./Index");
                }

                // Log the current state of the church object
                _logger.LogInformation("Updating church information for Church ID: {ChurchID}", church.ID);

                // Determine final parking value robustly (checkbox may not post when unchecked)
                bool finalParking = SaveChangesModel.Parking;
                try
                {
                    if (Request?.Form != null)
                    {
                        if (Request.Form.TryGetValue("SaveChangesModel.Parking", out var pv) && pv.Count > 0)
                        {
                            var parsed = pv[0].ToString();
                            if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                                finalParking = true;
                            else if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                                finalParking = false;
                        }
                        else if (Request.Form.TryGetValue("Parking", out var pv2) && pv2.Count > 0)
                        {
                            var parsed = pv2[0].ToString();
                            if (string.Equals(parsed, "on", StringComparison.OrdinalIgnoreCase) || string.Equals(parsed, "true", StringComparison.OrdinalIgnoreCase) || parsed == "1")
                                finalParking = true;
                            else if (string.Equals(parsed, "false", StringComparison.OrdinalIgnoreCase) || parsed == "0")
                                finalParking = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse Parking value from form; falling back to bound value.");
                }

                await _context.ChurchInformation
                    .Where(a => a.ID == church.ID)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(p => p.Name, SaveChangesModel.Name)
                        .SetProperty(p => p.Description, SaveChangesModel.Description)
                        .SetProperty(p => p.Denomination, SaveChangesModel.Denomination)
                        .SetProperty(p => p.Congregation, SaveChangesModel.Congregation)
                        .SetProperty(p => p.Address, SaveChangesModel.Address)
                        .SetProperty(p => p.Phone, SaveChangesModel.Phone)
                        .SetProperty(p => p.Churchsuite, SaveChangesModel.Churchsuite)
                        .SetProperty(p => p.Website, SaveChangesModel.Website)
                        .SetProperty(p => p.Parking, finalParking)
                        .SetProperty(p => p.Restrooms, SaveChangesModel.Restrooms)
                        .SetProperty(p => p.WheelchairAccess, SaveChangesModel.WheelchairAccess)
                        .SetProperty(p => p.Wifi, SaveChangesModel.Wifi)
                        .SetProperty(p => p.Refreshments, SaveChangesModel.Refreshments)
                        .SetProperty(p => p.HearingAidSupport, SaveChangesModel.HearingAidSupport)
                        .SetProperty(p => p.Kitchen, SaveChangesModel.Kitchen)
                        .SetProperty(p => p.Childcare, SaveChangesModel.Childcare)
                        .SetProperty(p => p.MeetingHall, SaveChangesModel.MeetingHall)
                        .SetProperty(p => p.OutdoorSpaces, SaveChangesModel.OutdoorSpaces)
                        .SetProperty(p => p.WorshipHall, SaveChangesModel.WorshipHall)
                    );

                await _context.SaveChangesAsync();

                // Events refer to their church by name; keep them attached after a rename.
                var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == church.ChurchAccountId);
                await ManagedChurches.RenameEventsAsync(_context, owner, church.Name, SaveChangesModel.Name);

                StatusMessage = $"Saved: {SaveChangesModel.Name} was updated.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while updating ({SaveChangesModel?.Name}).");
                StatusMessage = "Error: An unexpected error occurred. Please try again later.";
                return Page();
            }

            return RedirectToPage("./Index");
        }
    }
}