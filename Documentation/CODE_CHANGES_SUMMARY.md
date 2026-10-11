# Code Changes Summary - ChurchSuite Integration

## Overview
This document summarizes all code modifications made to integrate ChurchSuite API (Pull Calendar JSON Feed and GET /v1/profile) into ForestChurches.

---

## Files Modified

### 1. `Models\ChurchSuiteEventDto.cs`
**Status:** ✅ Enhanced with additional API fields

**Changes:**
- Added `[JsonPropertyName("venue")]` → `string? Venue`
- Added `[JsonPropertyName("url")]` → `string? Url`
- Added `[JsonPropertyName("image")]` → `string? Image`
- Added `[JsonPropertyName("image_thumb")]` → `string? ImageThumb`

**Rationale:** These fields are returned by ChurchSuite API and can enhance event display (venue info, event URLs, images).

---

### 2. `Components\Http\ChurchSuiteService.cs`
**Status:** ✅ Completely replaced with enhanced implementation

**Key Changes:**

#### Interface (`IChurchSuiteService`)
```csharp
public interface IChurchSuiteService
{
	Task<List<EventsModel>> GetCalendarEventsAsync(string accountId, string apiKey);
	Task<ChurchSuiteProfileResponse?> GetProfileAsync(string accountId, string apiKey);
	EventsModel MapChurchSuiteEventToEventsModel(ChurchSuiteEventDto churchSuiteEvent);
}
```

#### Constructor
```csharp
public ChurchSuiteService(HttpClient httpClient, ILogger<ChurchSuiteService> logger)
{
	_httpClient = httpClient;
	_logger = logger;
}
```
**Added:** ILogger injection for comprehensive logging

#### New Method: `GetProfileAsync`
- Endpoint: `GET https://{accountId}.churchsuite.com/api/v1/profile`
- Headers: X-Account, X-Authorization
- Returns: `ChurchSuiteProfileResponse` with account details
- Error handling with logging

#### Enhanced Method: `GetCalendarEventsAsync`
- Improved error handling
- Comprehensive logging (warnings, info, errors)
- Iterates through events with per-event error handling
- Returns empty list on validation failure instead of throwing

#### New Method: `MapChurchSuiteEventToEventsModel`
- Parses ISO 8601 datetime strings
- Handles null/empty field fallbacks
- Creates EventsModel with proper DateOnly/TimeOnly conversion
- Logs mapping errors per event

**Rationale:** Service now fully implements both ChurchSuite API endpoints with robust error handling and logging.

---

### 3. `Areas\Identity\Pages\Account\Register.cshtml.cs`
**Status:** ✅ Updated for ChurchSuite profile integration

**Constructor Changes:**
```csharp
private readonly IChurchSuiteService _churchSuiteService;

public RegisterModel(
	// ... existing parameters ...
	IChurchSuiteService churchSuiteService)
{
	// ... existing assignments ...
	_churchSuiteService = churchSuiteService;
}
```

**InputModel Changes:**
```csharp
[AllowNull]
[Display(Name = "ChurchSuite Account ID")]
public string ChurchSuiteAccountId { get; set; }

[AllowNull]
[DataType(DataType.Password)]
[Display(Name = "ChurchSuite API Key")]
public string ChurchSuiteApiKey { get; set; }
```

**OnPostAsync Enhancement:**
After `result.Succeeded`, added:
```csharp
// Attempt to populate profile from ChurchSuite if credentials are provided
if (!string.IsNullOrEmpty(Input.ChurchSuiteAccountId) && !string.IsNullOrEmpty(Input.ChurchSuiteApiKey))
{
	try
	{
		var profileResponse = await _churchSuiteService.GetProfileAsync(
			Input.ChurchSuiteAccountId, 
			Input.ChurchSuiteApiKey);

		if (profileResponse?.Account != null)
		{
			// Update church information
			if (!string.IsNullOrEmpty(profileResponse.Account.Name))
			{
				user.ChurchInformation.Name = profileResponse.Account.Name;
			}
			if (!string.IsNullOrEmpty(profileResponse.Account.Url) && 
				Uri.TryCreate(profileResponse.Account.Url, UriKind.Absolute, out var websiteUri))
			{
				user.ChurchInformation.Website = websiteUri;
			}
			if (!string.IsNullOrEmpty(profileResponse.Account.Email))
			{
				user.Email = profileResponse.Account.Email;
			}
			await _context.SaveChangesAsync();
			_logger.LogInformation("Successfully populated user profile from ChurchSuite...");
		}
	}
	catch (Exception ex)
	{
		_logger.LogWarning(ex, "Failed to fetch ChurchSuite profile...");
		// Don't fail registration if ChurchSuite API fails
	}
}
```

**Rationale:** Allows users to optionally provide ChurchSuite credentials during registration to auto-populate their church profile. Failures are non-fatal.

---

### 4. `Pages\Events\Index.cshtml.cs`
**Status:** ✅ Updated with sync endpoint

**Constructor Changes:**
```csharp
private readonly IChurchSuiteService _churchSuiteService;

public IndexModel(
	// ... existing parameters ...
	IChurchSuiteService churchSuiteService)
{
	// ... existing assignments ...
	_churchSuiteService = churchSuiteService;
}
```

**New Property:**
```csharp
[TempData]
public string StatusMessage { get; set; }
```

**New Method: `OnPostSyncFromChurchSuiteAsync`**
```csharp
[ValidateAntiForgeryToken]
public async Task<IActionResult> OnPostSyncFromChurchSuiteAsync(
	string churchSuiteAccountId, 
	string churchSuiteApiKey)
{
	try
	{
		// Validation
		if (string.IsNullOrEmpty(churchSuiteAccountId) || string.IsNullOrEmpty(churchSuiteApiKey))
		{
			StatusMessage = "Error: ChurchSuite account ID and API key are required.";
			return RedirectToPage();
		}

		CurrentUser = await _userManager.GetUserAsync(User);
		if (CurrentUser == null)
		{
			StatusMessage = "Error: Unable to find the current user.";
			return RedirectToPage();
		}

		// Fetch events from ChurchSuite
		var churchSuiteEvents = await _churchSuiteService.GetCalendarEventsAsync(
			churchSuiteAccountId, 
			churchSuiteApiKey);

		if (churchSuiteEvents == null || churchSuiteEvents.Count == 0)
		{
			StatusMessage = "No events found in ChurchSuite or sync completed with no new events.";
			return RedirectToPage();
		}

		int importedCount = 0;
		int skippedCount = 0;

		foreach (var churchSuiteEvent in churchSuiteEvents)
		{
			try
			{
				// Check for duplicates (Name + Date + User)
				var existingEvent = _context.Events.FirstOrDefault(e => 
					e.Name == churchSuiteEvent.Name && 
					e.Date == churchSuiteEvent.Date && 
					e.User == CurrentUser.Email);

				if (existingEvent == null)
				{
					churchSuiteEvent.User = CurrentUser.Email;
					_context.Events.Add(churchSuiteEvent);
					importedCount++;
				}
				else
				{
					skippedCount++;
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to import event {EventName}...", churchSuiteEvent.Name);
				skippedCount++;
			}
		}

		if (importedCount > 0)
		{
			await _context.SaveChangesAsync();
		}

		StatusMessage = $"ChurchSuite sync completed. Imported {importedCount} events, skipped {skippedCount} duplicate(s).";
		_logger.LogInformation("User {UserId} successfully synced {Count} events...", CurrentUser.Id, importedCount);
	}
	catch (HttpRequestException ex)
	{
		_logger.LogError(ex, "HTTP error while syncing events...");
		StatusMessage = "Error: Failed to connect to ChurchSuite...";
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "Unexpected error while syncing events...");
		StatusMessage = $"Error: An unexpected error occurred during sync. {ex.Message}";
	}

	return RedirectToPage();
}
```

**Rationale:** Provides on-demand event sync endpoint with duplicate detection and detailed status reporting.

---

### 5. `Areas\Identity\Pages\Account\Manage\ChurchProfile.cshtml.cs`
**Status:** ✅ Updated for manual profile sync

**Constructor Changes:**
```csharp
private readonly IChurchSuiteService _churchSuiteService;

public ChurchProfileModel(
	// ... existing parameters ...
	IChurchSuiteService churchSuiteService)
{
	// ... existing assignments ...
	_churchSuiteService = churchSuiteService ?? throw new ArgumentNullException(nameof(churchSuiteService));
}
```

**New Method: `OnPostSyncChurchSuiteProfileAsync`**
```csharp
[ValidateAntiForgeryToken]
public async Task<IActionResult> OnPostSyncChurchSuiteProfileAsync(
	string churchSuiteAccountId, 
	string churchSuiteApiKey)
{
	try
	{
		// Validation
		if (string.IsNullOrEmpty(churchSuiteAccountId) || string.IsNullOrEmpty(churchSuiteApiKey))
		{
			StatusMessage = "Error: ChurchSuite account ID and API key are required.";
			return RedirectToPage();
		}

		CurrentUser = await _userManager.GetUserAsync(User);
		if (CurrentUser == null)
		{
			StatusMessage = "Error: Unable to find the current user.";
			return RedirectToPage();
		}

		CurrentChurch = await _context.ChurchInformation
			.FirstOrDefaultAsync(c => c.ChurchAccountId == CurrentUser.Id);
		if (CurrentChurch == null)
		{
			StatusMessage = "Error: Church information is missing.";
			return RedirectToPage();
		}

		// Fetch profile from ChurchSuite
		var profileResponse = await _churchSuiteService.GetProfileAsync(
			churchSuiteAccountId, 
			churchSuiteApiKey);

		if (profileResponse?.Account == null)
		{
			StatusMessage = "Error: Unable to retrieve profile...";
			return RedirectToPage();
		}

		// Selective update
		bool updated = false;

		if (!string.IsNullOrEmpty(profileResponse.Account.Name))
		{
			CurrentChurch.Name = profileResponse.Account.Name;
			updated = true;
		}

		if (!string.IsNullOrEmpty(profileResponse.Account.Url) && 
			Uri.TryCreate(profileResponse.Account.Url, UriKind.Absolute, out var websiteUri))
		{
			CurrentChurch.Website = websiteUri;
			updated = true;
		}

		if (!string.IsNullOrEmpty(profileResponse.Account.Email))
		{
			CurrentUser.Email = profileResponse.Account.Email;
			CurrentUser.UserName = profileResponse.Account.Email;
			updated = true;
		}

		if (updated)
		{
			_context.Update(CurrentChurch);
			await _userManager.UpdateAsync(CurrentUser);
			await _context.SaveChangesAsync();
			StatusMessage = "Church profile successfully synced from ChurchSuite!";
			_logger.LogInformation("User {UserId} successfully synced church profile...", CurrentUser.Id);
		}
		else
		{
			StatusMessage = "Warning: ChurchSuite profile retrieved but no updates were made.";
		}

		// Reload the model for display
		PopulateSaveChangesModel();
	}
	catch (HttpRequestException ex)
	{
		_logger.LogError(ex, "HTTP error while syncing profile...");
		StatusMessage = "Error: Failed to connect to ChurchSuite...";
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "Unexpected error while syncing profile...");
		StatusMessage = $"Error: An unexpected error occurred during sync. {ex.Message}";
	}

	return RedirectToPage();
}
```

**Rationale:** Allows users to manually refresh their church profile from ChurchSuite with selective updates and proper error handling.

---

## Files Not Modified But Important

### `Components\Configuration\ServiceRegistration.cs`
**Status:** ℹ️ Already contains registration
```csharp
Services.AddHttpClient<IChurchSuiteService, ChurchSuiteService>();
```
No changes needed - service properly registered with HttpClientFactory.

### `Models\ChurchSuiteProfileResponse.cs`
**Status:** ℹ️ Already present
Used by GetProfileAsync - contains Account property of type ChurchSuiteAccountDetails.

### `Models\ChurchSuiteAccountDetails.cs`
**Status:** ℹ️ Already present
Contains Name, Email, Url and other account information from ChurchSuite API.

---

## API Integration Details

### ChurchSuite API Endpoints Used

#### 1. Pull Calendar JSON Feed
```
GET https://{accountId}.churchsuite.com/api/v1/calendar/export
Headers:
  X-Account: {accountId}
  X-Authorization: {apiKey}

Response: JSON array of events
[
  {
	"id": "123",
	"name": "Sunday Service",
	"description": "Regular worship service",
	"datetime_start": "2024-12-25T10:30:00Z",
	"datetime_end": "2024-12-25T11:30:00Z",
	"location": "Main Hall",
	"venue": "Church Building",
	"url": "https://example.com/event/123",
	"image": "...",
	"image_thumb": "..."
  }
]
```

#### 2. GET /v1/profile
```
GET https://{accountId}.churchsuite.com/api/v1/profile
Headers:
  X-Account: {accountId}
  X-Authorization: {apiKey}

Response: JSON object
{
  "account": {
	"id": "abc123",
	"name": "Grace Community Church",
	"email": "contact@gracechurch.co.uk",
	"url": "https://gracechurch.co.uk",
	...
  }
}
```

---

## Testing Implementation

### Build Verification
✅ Solution builds successfully with no errors
✅ All dependencies properly injected
✅ Service registration verified

### Compilation Output
```
Build succeeded. 0 Warning(s), 0 Error(s)
```

---

## Key Design Decisions

1. **Non-Fatal ChurchSuite Failures**
   - Registration completes even if ChurchSuite API is unavailable
   - Events sync continues even if individual event import fails
   - Ensures application resilience to external service downtime

2. **Selective Profile Update**
   - Only fields with valid data are updated
   - URI validation ensures Website field doesn't contain invalid data
   - Null/empty fields are skipped

3. **Duplicate Detection**
   - Events checked by Name + Date + User combination
   - Prevents duplicate imports on re-sync
   - Simple and performant

4. **Comprehensive Logging**
   - Info level: Successful operations and counts
   - Warning level: Non-fatal errors, unexpected conditions
   - Error level: API failures, parsing errors
   - Aids in troubleshooting without exposing internals

5. **DateTime Handling**
   - Assumes ChurchSuite returns ISO 8601 format
   - Parsed into DateOnly and TimeOnly for consistency
   - Handles parsing errors gracefully

---

## Migration Notes

No database migrations required - uses existing EventsModel and ChurchInformation tables.

---

## Backward Compatibility

✅ All changes are backward compatible
- New optional fields on DTOs
- Optional credentials in registration
- New optional sync methods
- No breaking changes to existing APIs

---

## Performance Considerations

- `GetCalendarEventsAsync`: Minimal impact, one API call per sync
- `GetProfileAsync`: Single API call during registration or manual sync
- Event duplicate check uses LINQ query (indexed on Name, Date, User recommended)
- Per-event error handling doesn't block bulk operations

---

## Security Considerations

- API keys never logged (credentials handled carefully)
- Input validation on credentials before API calls
- HTTPS enforced by ChurchSuite API URLs
- URI validation prevents malicious Website field injection
- All user input from API responses is escaped by EF Core on display

---

## Next Steps for Completion

1. ✅ Backend implementation completed
2. ⏳ Add UI sections to Register.cshtml, Events/Index.cshtml, ChurchProfile.cshtml
3. ⏳ End-to-end testing with real ChurchSuite credentials
4. ⏳ User documentation and support materials
5. ⏳ Optional: Implement background job for scheduled syncs

See `IMPLEMENTATION_CHECKLIST.md` for detailed UI implementation guidance.

---

**Implementation Date:** 2024
**Status:** Backend Complete ✅ | UI Pending ⏳ | Testing Pending ⏳
