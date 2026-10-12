using ForestChurches.Components.Email;
using ForestChurches.Components.FileManager;
using ForestChurches.Components.Http.Google;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ForestChurches.Components.Users;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ForestChurches.Pages
{
    public class NearMeModel : PageModel
    {
        private readonly GoogleInterface _googleAPI;
        private readonly PostcodeValidatorInterface _iPostcode;
        private readonly FileInterface _fileInterface;
        private readonly ForestChurches.Components.Security.Encoder _Encoder;
        private readonly ILogger<NearMeModel> _logger;
        private readonly ForestChurchesContext _context;
        private readonly iMailSender _mailRepository;
        private readonly UserManager<ChurchAccount> _userManager;

        internal List<ChurchInformation> _places;
        internal List<EventsModel> _events;
        internal List<OrganizedEvents> _organisedEvents;
        internal List<Guid> cid;
        internal string emailAddress;

        [BindProperty]
        public string Postcode { get; set; }

        // Denominations (single-select)
        [ValidateNever]
        [BindProperty]
        public string? SelectedDenomination { get; set; }

        public List<string> Denominations { get; set; } = new();

        [BindProperty]
        public bool FilterParking { get; set; } = false;

        [BindProperty]
        public bool FilterRestrooms { get; set; } = false;

        [BindProperty]
        public bool FilterWheelchairAccess { get; set; } = false;

        [BindProperty]
        public bool FilterWifi { get; set; } = false;

        [BindProperty]
        public bool FilterRefreshments { get; set; } = false;

        [BindProperty]
        public bool FilterWorshipHall { get; set; } = false;

        [BindProperty]
        public bool FilterChildcare { get; set; } = false;

        [BindProperty]
        public bool FilterMeetingHall { get; set; } = false;

        [BindProperty]
        public bool FilterKitchen { get; set; } = false;

        [BindProperty]
        public bool FilterOutdoorSpaces { get; set; } = false;

        [BindProperty]
        public bool FilterHearingAidSupport { get; set; } = false;

        [BindProperty]
        public double? MaxDistanceMiles { get; set; } = 5.0;

        // Event Filters
        [ValidateNever]
        [BindProperty]
        public string? SelectedChurch { get; set; }
        public List<string> ChurchNames { get; set; } = new();

        [BindProperty]
        public DateTime? EventStartDate { get; set; }
        [BindProperty]
        public DateTime? EventEndDate { get; set; }

        [TempData]
        internal bool isAuthenticated { get; set; }
        internal bool ShowPlacesModal { get; set; }
        internal bool ShowEventsModal { get; set; }

        // Distance (in miles) from the searched location, keyed by church ID.
        internal Dictionary<string, double> PlaceDistanceMiles { get; } = new();

        // Coordinates of each result (keyed by church ID) and of the searched location, for the results map.
        internal Dictionary<string, (double Lat, double Lng)> PlaceCoordinates { get; } = new();
        internal (double Lat, double Lng)? SearchCoordinates { get; set; }

        public NearMeModel(
            GoogleInterface GoogleInterface,
            FileInterface fileInterface,
            PostcodeValidatorInterface iPostcode,
            ForestChurchesContext context,
            iMailSender mailRepository,
            UserManager<ChurchAccount> userManager,
            ILogger<NearMeModel> logger,
            ForestChurches.Components.Security.Encoder Encoder)
        {
            _googleAPI = GoogleInterface;
            _context = context;
            _mailRepository = mailRepository;
            _fileInterface = fileInterface;
            _iPostcode = iPostcode;
            _userManager = userManager;
            _logger = logger;
            _Encoder = Encoder;

            cid = new();
            _places = new();
            _events = new();
            _organisedEvents = new();
        }

        public async Task<IActionResult> OnGetAsync()
        {
            TempData["Message"] = null;
            await LoadLookupsAsync();
            return Page();
        }

        // Populates the denomination and church dropdowns so they survive a re-render after any POST.
        private async Task LoadLookupsAsync()
        {
            Denominations = await _context.ChurchInformation
                .Select(c => c.Denomination)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();

            ChurchNames = await _context.ChurchInformation
                .Where(c => c.Status == "Open")
                .Select(c => c.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostChurchForm()
        {
            await LoadLookupsAsync();
            if (!ModelState.IsValid)
            {
                _logger.LogInformation("ModelState invalid in OnPostChurchForm");

                var errors = new List<string>();
                foreach (var kv in ModelState)
                {
                    var key = kv.Key;
                    foreach (var err in kv.Value.Errors)
                    {
                        var msg = string.IsNullOrEmpty(err.ErrorMessage) ? err.Exception?.Message : err.ErrorMessage;
                        errors.Add($"{key}: {msg}");
                        _logger.LogWarning("ModelState error for {Key}: {Error}", key, msg);
                    }
                }

                // Show visitors a plain message rather than raw validation keys.
                TempData["Message"] = string.IsNullOrWhiteSpace(Postcode)
                    ? "Please enter a postcode or town to search."
                    : "Please check your search and try again.";

                return Page();
            }

            try
            {
                var postcode = Postcode?.ToUpper() ?? string.Empty;
                ViewData["postcode"] = postcode;

                // ensure denominations available for re-render
                Denominations = await _context.ChurchInformation
                    .Select(c => c.Denomination)
                    .Distinct()
                    .OrderBy(d => d)
                    .ToListAsync();

                // Log received filter values
                var effectiveMaxDistance = MaxDistanceMiles ?? 5.0;

                _logger.LogInformation("OnPostChurchForm called. Postcode={Postcode} MaxDistance={MaxDistance} SelectedDenomination={Denomination} Filters: Parking={Parking} Restrooms={Restrooms} Wheelchair={Wheelchair} Wifi={Wifi} Refreshments={Refreshments} WorshipHall={WorshipHall} Childcare={Childcare} MeetingHall={MeetingHall} Kitchen={Kitchen} Outdoor={Outdoor} HearingAid={HearingAid}",
                    postcode, effectiveMaxDistance, SelectedDenomination,
                    FilterParking, FilterRestrooms, FilterWheelchairAccess, FilterWifi, FilterRefreshments, FilterWorshipHall, FilterChildcare, FilterMeetingHall, FilterKitchen, FilterOutdoorSpaces, FilterHearingAidSupport);

                // Build base query and apply facility / denomination filters in SQL
                var query = _context.ChurchInformation.Include(c => c.ServiceTimes).Where(c => c.Status == "Open");

                if (FilterParking) query = query.Where(c => c.Parking);
                if (FilterRestrooms) query = query.Where(c => c.Restrooms);
                if (FilterWheelchairAccess) query = query.Where(c => c.WheelchairAccess);
                if (FilterWifi) query = query.Where(c => c.Wifi);
                if (FilterRefreshments) query = query.Where(c => c.Refreshments);
                if (FilterWorshipHall) query = query.Where(c => c.WorshipHall);
                if (FilterChildcare) query = query.Where(c => c.Childcare);
                if (FilterMeetingHall) query = query.Where(c => c.MeetingHall);
                if (FilterKitchen) query = query.Where(c => c.Kitchen);
                if (FilterOutdoorSpaces) query = query.Where(c => c.OutdoorSpaces);
                if (FilterHearingAidSupport) query = query.Where(c => c.HearingAidSupport);

                if (!string.IsNullOrEmpty(SelectedDenomination))
                {
                    query = query.Where(c => c.Denomination == SelectedDenomination);
                }

                var candidateChurches = await query.ToListAsync(); // reduced result set
                _logger.LogInformation("Candidate churches after DB filter: {Count}", candidateChurches.Count);

                // Resolve coordinates from the provided input (postcode or full address)
                double[] inputCoordinates;
                try
                {
                    inputCoordinates = await ResolveCoordinatesAsync(postcode).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve coordinates from input '{Input}'", postcode);
                    TempData["Message"] = "Unable to interpret the provided address/postcode. Try a shorter postcode or a more specific address.";
                    return Page();
                }

                double latitude = inputCoordinates[0];
                double longitude = inputCoordinates[1];

                var orderedChurches = new List<(ChurchInformation Church, double Distance)>();
                double maxDistanceKm = effectiveMaxDistance * 1.60934;

                SearchCoordinates = (latitude, longitude);
                foreach (var church in candidateChurches)
                {
                    double[] churchCoordinates;
                    try
                    {
                        churchCoordinates = await _googleAPI.ConvertToCoordinates(church.Address);
                    }
                    catch (Exception ex)
                    {
                        // One church with a bad address shouldn't break the whole search.
                        _logger.LogWarning(ex, "Unable to geocode church {ChurchId}", church.ID);
                        continue;
                    }

                    double distance = HavarsineDistance(latitude, longitude, churchCoordinates[0], churchCoordinates[1]);
                    if (distance <= maxDistanceKm)
                    {
                        orderedChurches.Add((church, distance));
                        PlaceCoordinates[church.ID] = (churchCoordinates[0], churchCoordinates[1]);
                    }
                }

                orderedChurches = orderedChurches.OrderBy(x => x.Distance).ToList();
                _logger.LogInformation("Churches within distance: {Count}", orderedChurches.Count);

                cid = orderedChurches.Select(c => Guid.Parse(c.Church.ID)).ToList();
                _places = orderedChurches.Select(x => x.Church).ToList();
                foreach (var (church, distanceKm) in orderedChurches)
                {
                    PlaceDistanceMiles[church.ID] = distanceKm / 1.60934;
                }
                ShowPlacesModal = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnPostChurchForm");
                // Surface a minimal message to user via TempData and return Page so they see something
                TempData["Message"] = "An error occurred while searching for nearby churches. Please try again.";
            }

            return Page();
        }

        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostEventForm()
        {
            await LoadLookupsAsync();
            if (!ModelState.IsValid)
            {
                TempData["Message"] = string.IsNullOrWhiteSpace(Postcode)
                    ? "Please enter a postcode or town to search for events."
                    : "Please check your search and try again.";
                return Page();
            }

            try
            {
                var postcode = Postcode?.ToUpper() ?? string.Empty;
                ViewData["postcode"] = postcode;

                double[] userCoordinates;
                try
                {
                    userCoordinates = await ResolveCoordinatesAsync(postcode).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve coordinates from input '{Input}'", postcode);
                    TempData["Message"] = "Unable to interpret the provided address/postcode. Try a shorter postcode or a more specific address.";
                    return Page();
                }
                double userLatitude = userCoordinates[0];
                double userLongitude = userCoordinates[1];

                // Filter events by selected church if provided
                var eventsQuery = _context.Events
                    .Where(e => _context.ChurchInformation.Any(c => c.Name == e.Church && c.Status == "Open"));

                if (!string.IsNullOrEmpty(SelectedChurch))
                {
                    eventsQuery = eventsQuery.Where(e => e.Church == SelectedChurch);
                }

                if (EventStartDate.HasValue)
                {
                    var startDate = DateOnly.FromDateTime(EventStartDate.Value);
                    eventsQuery = eventsQuery.Where(e => e.Date >= startDate);
                }
                if (EventEndDate.HasValue)
                {
                    var endDate = DateOnly.FromDateTime(EventEndDate.Value);
                    eventsQuery = eventsQuery.Where(e => e.Date <= endDate);
                }

                var events = await eventsQuery.ToListAsync();
                var eventDistances = new List<(EventsModel Event, double Distance)>();

                double maxDistanceKm = (MaxDistanceMiles ?? 5.0) * 1.60934;

                foreach (var ev in events)
                {
                    try
                    {
                        double[] eventCoords = await _googleAPI.ConvertToCoordinates(ev.Address);
                        double distance = HavarsineDistance(userLatitude, userLongitude, eventCoords[0], eventCoords[1]);
                        if (distance <= maxDistanceKm)
                        {
                            eventDistances.Add((ev, distance));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Unable to geocode event address for event {EventId}", ev.ID);
                        // skip events we can't geocode
                    }
                }

                eventDistances = eventDistances.OrderBy(ed => ed.Distance).ToList();
                _events = eventDistances.Select(ed => ed.Event).ToList();
                ShowEventsModal = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnPostEventForm");
                TempData["Message"] = "An error occurred while searching for nearby events. Please try again.";
            }

            return Page();
        }

        private double HavarsineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double RadiusKm = 6371.0;
            const double DegToRad = Math.PI / 180.0;

            double dLat = (lat2 - lat1) * DegToRad;
            double dLon = (lon2 - lon1) * DegToRad;

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(lat1 * DegToRad) * Math.Cos(lat2 * DegToRad) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Asin(Math.Sqrt(a));
            return RadiusKm * c;
        }

        private async Task<double[]> ResolveCoordinatesAsync(string input)
        {
            // Try the input directly first (works if user provided a postcode or well-formed address)
            try
            {
                var coords = await _googleAPI.ConvertToCoordinates(input).ConfigureAwait(true);
                if (coords != null && coords.Length == 2 && !double.IsNaN(coords[0]) && !double.IsNaN(coords[1]))
                {
                    return coords;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Direct geocode failed for input '{Input}'", input);
            }

            // If direct resolution fails, try to extract a postcode-like token from the input
            try
            {
                var postcodeCandidate = ExtractPostcodeLikeToken(input);
                if (!string.IsNullOrEmpty(postcodeCandidate))
                {
                    var coords = await _googleAPI.ConvertToCoordinates(postcodeCandidate).ConfigureAwait(true);
                    if (coords != null && coords.Length == 2)
                    {
                        return coords;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Postcode-token geocode failed for input '{Input}'", input);
            }

            // As a last resort, attempt incremental address reduction: split by commas and try from most-specific to least-specific
            var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList();
            for (int i = 0; i < parts.Count; i++)
            {
                var candidate = string.Join(", ", parts.Skip(i));
                try
                {
                    var coords = await _googleAPI.ConvertToCoordinates(candidate).ConfigureAwait(true);
                    if (coords != null && coords.Length == 2)
                    {
                        return coords;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Incremental geocode failed for candidate '{Candidate}' from input '{Input}'", candidate, input);
                }
            }

            throw new InvalidOperationException($"Unable to resolve coordinates for input '{input}'");
        }

        private string? ExtractPostcodeLikeToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Typical UK postcode pattern (simplified) and some generic numeric postal codes.
            var postcodePattern = new System.Text.RegularExpressions.Regex(@"([A-Z]{1,2}\d{1,2}[A-Z]?\s*\d[A-Z]{2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var match = postcodePattern.Match(input);
            if (match.Success)
            {
                return match.Value.Trim();
            }

            // Generic numeric postal code (e.g., US ZIP)
            var zipPattern = new System.Text.RegularExpressions.Regex(@"\b\d{5}(?:-\d{4})?\b");
            var zipMatch = zipPattern.Match(input);
            if (zipMatch.Success)
            {
                return zipMatch.Value;
            }

            return null;
        }

        public async Task<IActionResult> OnGetGetAddress(double latitude, double longitude)
        {
            try
            {
                string address = await _googleAPI.ConvertToAddress(latitude, longitude);
                return Content(address, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error converting coordinates to address");
                return Content("Unable to retrieve address", "text/plain");
            }
        }
    }
}
