# ChurchSuite API Integration Guide for ForestChurches

## Overview
This guide documents the complete integration of ChurchSuite API into the ForestChurches Razor Pages application, enabling users to:
1. **Import events** from ChurchSuite calendar feeds into their Events section
2. **Populate church profile** from ChurchSuite account details during registration or via manual sync

## Architecture

### Core Components

#### 1. **ChurchSuiteService** (`Components\Http\ChurchSuiteService.cs`)
The main integration point providing three key methods:

- **`GetCalendarEventsAsync(accountId, apiKey)`**
  - Fetches calendar events from ChurchSuite API
  - Endpoint: `GET https://{accountId}.churchsuite.com/api/v1/calendar/export`
  - Returns: `Task<List<EventsModel>>`
  - Automatically maps ChurchSuite DTOs to internal EventsModel

- **`GetProfileAsync(accountId, apiKey)`**
  - Fetches church profile information
  - Endpoint: `GET https://{accountId}.churchsuite.com/api/v1/profile`
  - Returns: `Task<ChurchSuiteProfileResponse?>`
  - Contains account name, email, URL, and other details

- **`MapChurchSuiteEventToEventsModel(churchSuiteEvent)`**
  - Converts ChurchSuiteEventDto to EventsModel
  - Handles DateTime parsing (ISO format to DateOnly/TimeOnly)
  - Manages null/empty field fallbacks

**Headers Required:**
```
X-Account: {accountId}
X-Authorization: {apiKey}
```

#### 2. **Data Models**

**ChurchSuiteEventDto** (`Models\ChurchSuiteEventDto.cs`)
```csharp
public class ChurchSuiteEventDto
{
	[JsonPropertyName("id")] public object? Id { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; }
	[JsonPropertyName("description")] public string? Description { get; set; }
	[JsonPropertyName("datetime_start")] public string DateTimeStart { get; set; }
	[JsonPropertyName("datetime_end")] public string DateTimeEnd { get; set; }
	[JsonPropertyName("location")] public string? Location { get; set; }
	[JsonPropertyName("venue")] public string? Venue { get; set; }
	[JsonPropertyName("url")] public string? Url { get; set; }
	[JsonPropertyName("image")] public string? Image { get; set; }
	[JsonPropertyName("image_thumb")] public string? ImageThumb { get; set; }
}
```

**ChurchSuiteProfileResponse** (`Models\ChurchSuiteProfileResponse.cs`)
Contains wrapper for profile data with nested ChurchSuiteAccountDetails object.

#### 3. **Dependency Injection**
Service is registered in `Components\Configuration\ServiceRegistration.cs`:
```csharp
Services.AddHttpClient<IChurchSuiteService, ChurchSuiteService>();
```

HttpClient factory automatically provides HttpClient instance; ILogger injected by DI container.

---

## Integration Points

### 1. **User Registration** (`Areas\Identity\Pages\Account\Register.cshtml.cs`)

**New InputModel Properties:**
```csharp
[AllowNull]
[Display(Name = "ChurchSuite Account ID")]
public string ChurchSuiteAccountId { get; set; }

[AllowNull]
[DataType(DataType.Password)]
[Display(Name = "ChurchSuite API Key")]
public string ChurchSuiteApiKey { get; set; }
```

**Profile Population Flow (OnPostAsync):**
```
1. User registration succeeds
2. IF ChurchSuite credentials provided:
   - Call GetProfileAsync(accountId, apiKey)
   - Parse profile.Account.Url to Uri (with Uri.TryCreate validation)
   - Update ChurchInformation.Name, Website, user.Email
   - SaveChangesAsync
3. IF ChurchSuite call fails: Log warning and continue (non-fatal)
4. Send confirmation email
```

**Key Features:**
- Optional: Users can register without ChurchSuite credentials
- Safe: Registration completes even if ChurchSuite API is unavailable
- Selective: Only updates fields if ChurchSuite data is present and valid

### 2. **Events Page** (`Pages\Events\Index.cshtml.cs`)

**New Method: `OnPostSyncFromChurchSuiteAsync`**
```csharp
[ValidateAntiForgeryToken]
public async Task<IActionResult> OnPostSyncFromChurchSuiteAsync(
	string churchSuiteAccountId, 
	string churchSuiteApiKey)
```

**Sync Workflow:**
```
1. Validate credentials provided
2. Fetch events: GetCalendarEventsAsync(accountId, apiKey)
3. For each ChurchSuite event:
   - Check if duplicate exists (Name + Date + User match)
   - If new: Add to _context.Events
   - If duplicate: Skip and count
4. SaveChangesAsync
5. Return StatusMessage with import stats
```

**Duplicate Detection:**
```csharp
var existingEvent = _context.Events.FirstOrDefault(e => 
	e.Name == churchSuiteEvent.Name && 
	e.Date == churchSuiteEvent.Date && 
	e.User == CurrentUser.Email);
```

### 3. **Church Profile** (`Areas\Identity\Pages\Account\Manage\ChurchProfile.cshtml.cs`)

**New Method: `OnPostSyncChurchSuiteProfileAsync`**
```csharp
[ValidateAntiForgeryToken]
public async Task<IActionResult> OnPostSyncChurchSuiteProfileAsync(
	string churchSuiteAccountId, 
	string churchSuiteApiKey)
```

**Profile Refresh Flow:**
```
1. Validate credentials
2. Fetch profile: GetProfileAsync(accountId, apiKey)
3. Selectively update ChurchInformation:
   - Name (if provided)
   - Website (if valid URI)
   - User Email and UserName (if provided)
4. UpdateAsync user and SaveChangesAsync
5. Reload model for display
6. Return StatusMessage with result
```

**Fields Updated:**
- `ChurchInformation.Name`
- `ChurchInformation.Website` (converted from string to Uri)
- `ChurchAccount.Email` and `ChurchAccount.UserName`

---

## Event Mapping Details

### DateTime Parsing
ChurchSuite API returns ISO 8601 format: `"2024-12-25T10:30:00Z"`

Mapping process:
```csharp
DateTime.TryParse(churchSuiteEvent.DateTimeStart, out var startDateTime)
// Converts to:
DateOnly.FromDateTime(startDateTime)  // Date part
TimeOnly.FromDateTime(startDateTime)  // Time part
```

### Field Mapping
```
ChurchSuite Field      → EventsModel Field
─────────────────────────────────────────
id                     → ID
name                   → Name
description (nullable) → Description ("No description available" if null)
datetime_start         → Date + StartTime
datetime_end           → EndTime
location || venue      → Address ("TBA" if both null)
url                    → Link
```

---

## Error Handling & Logging

### Service Logging Levels
- **Warning**: Empty credentials, no events returned
- **Information**: Successful API calls, event counts, profile syncs
- **Error**: Network failures, parsing errors, unexpected exceptions

### Exception Handling Strategy
1. **HTTP Errors**: Logged and re-thrown (lets page model handle)
2. **Parsing Errors**: Logged per-event; continues processing remaining events
3. **Registration**: ChurchSuite errors never fail registration
4. **Events Sync**: Individual event failures don't stop bulk import

### User-Facing Messages
- Success: `"ChurchSuite sync completed. Imported X events, skipped Y duplicate(s)."`
- Failure: `"Error: Failed to connect to ChurchSuite. Please check your credentials and try again."`
- Warning: `"Warning: ChurchSuite profile retrieved but no updates were made."`

---

## UI Integration (Views)

### Register Page (`Areas\Identity\Pages\Account\Register.cshtml`)
Add optional form fields:
```html
<div class="form-group">
	<label asp-for="Input.ChurchSuiteAccountId"></label>
	<input asp-for="Input.ChurchSuiteAccountId" class="form-control" placeholder="Optional" />
	<small class="form-text text-muted">Leave blank to skip ChurchSuite profile import</small>
</div>

<div class="form-group">
	<label asp-for="Input.ChurchSuiteApiKey"></label>
	<input asp-for="Input.ChurchSuiteApiKey" class="form-control" type="password" placeholder="Optional" />
	<small class="form-text text-muted">Your ChurchSuite API credentials (kept secure)</small>
</div>
```

### Events Page (`Pages\Events\Index.cshtml`)
Add sync form:
```html
<div class="card mb-3">
	<div class="card-header">
		<h5>Import from ChurchSuite</h5>
	</div>
	<div class="card-body">
		<form method="post" asp-page-handler="SyncFromChurchSuite">
			<div class="form-group">
				<label for="churchSuiteAccountId">ChurchSuite Account ID</label>
				<input type="text" id="churchSuiteAccountId" name="churchSuiteAccountId" 
					   class="form-control" required />
			</div>
			<div class="form-group">
				<label for="churchSuiteApiKey">ChurchSuite API Key</label>
				<input type="password" id="churchSuiteApiKey" name="churchSuiteApiKey" 
					   class="form-control" required />
			</div>
			<button type="submit" class="btn btn-primary">Sync Events</button>
		</form>
	</div>
</div>

@if (!string.IsNullOrEmpty(Model.StatusMessage))
{
	<div class="alert alert-info alert-dismissible fade show" role="alert">
		@Model.StatusMessage
		<button type="button" class="btn-close" data-bs-dismiss="alert"></button>
	</div>
}
```

### Church Profile Page (`Areas\Identity\Pages\Account\Manage\ChurchProfile.cshtml`)
Add manual sync button:
```html
<div class="card mb-3">
	<div class="card-header">
		<h5>Sync Profile from ChurchSuite</h5>
	</div>
	<div class="card-body">
		<form method="post" asp-page-handler="SyncChurchSuiteProfile">
			<div class="form-group">
				<label for="churchSuiteAccountId">ChurchSuite Account ID</label>
				<input type="text" id="churchSuiteAccountId" name="churchSuiteAccountId" 
					   class="form-control" required />
			</div>
			<div class="form-group">
				<label for="churchSuiteApiKey">ChurchSuite API Key</label>
				<input type="password" id="churchSuiteApiKey" name="churchSuiteApiKey" 
					   class="form-control" required />
			</div>
			<button type="submit" class="btn btn-primary">Refresh Profile</button>
		</form>
	</div>
</div>

@if (!string.IsNullOrEmpty(Model.StatusMessage))
{
	<div class="alert alert-info alert-dismissible fade show" role="alert">
		@Model.StatusMessage
		<button type="button" class="btn-close" data-bs-dismiss="alert"></button>
	</div>
}
```

---

## API Credentials Setup

### Obtaining ChurchSuite API Key
1. Log in to your ChurchSuite admin account
2. Navigate to **Settings → Integrations → API**
3. Generate a new API key (or copy existing one)
4. Copy Account ID from dashboard or URL
5. **Keep API key secure** - treat as password

### Account ID
- Found in ChurchSuite dashboard URL or admin settings
- Format: typically a short alphanumeric code
- Example: `abc123` (in URL: `https://abc123.churchsuite.com`)

---

## Testing Checklist

- [ ] **Registration with ChurchSuite Credentials**
  - Register new church with valid ChurchSuite credentials
  - Verify profile name, website, and email are populated from ChurchSuite
  - Register without credentials (should succeed)
  - Attempt with invalid credentials (should fail gracefully, registration succeeds)

- [ ] **Event Sync**
  - Login to user account
  - Navigate to Events page
  - Enter valid ChurchSuite credentials
  - Click "Sync Events"
  - Verify events appear in Events list
  - Run sync again (verify duplicates are skipped)

- [ ] **Manual Profile Sync**
  - Navigate to Church Profile page
  - Enter ChurchSuite credentials
  - Click "Refresh Profile"
  - Verify Name, Website, Email update with ChurchSuite data
  - Test with invalid credentials

- [ ] **Error Handling**
  - Test with expired API key (should show error)
  - Test with wrong account ID (should show error)
  - Test with missing credentials (should validate and reject)
  - Verify application logs contain appropriate error/warning messages

---

## Future Enhancements

### Scheduled Sync
Implement background job (Hangfire/hosted service) to automatically sync events:
```csharp
// Example: Sync every 6 hours
RecurringJob.AddOrUpdate<IChurchSuiteService>(
	"sync-churchsuite-events",
	svc => svc.GetCalendarEventsAsync(accountId, apiKey),
	"0 */6 * * *");
```

### Advanced Duplicate Detection
Enhance to check ChurchSuite event ID if available:
```csharp
var existingEvent = _context.Events.FirstOrDefault(e =>
	e.ExternalEventId == churchSuiteEvent.Id.ToString() &&
	e.Source == "ChurchSuite");
```

### Credential Storage
Store encrypted ChurchSuite credentials in user profile:
```csharp
public class ChurchAccount
{
	[Encrypted]
	public string ChurchSuiteAccountId { get; set; }

	[Encrypted]
	public string ChurchSuiteApiKey { get; set; }
}
```

### Webhook Integration
Accept ChurchSuite webhooks for real-time event updates instead of polling.

### Multi-Sync Operations
Allow selecting date range or specific calendars to import from.

---

## Troubleshooting

### "Failed to connect to ChurchSuite"
- Verify credentials are correct
- Check API key hasn't been revoked
- Ensure Account ID is in correct format
- Check if ChurchSuite API service is operational

### No Events Imported
- Verify ChurchSuite calendar has events within date range
- Check if events have proper datetime_start/datetime_end values
- Review application logs for parsing errors
- Ensure API key has Calendar permission

### Profile Not Updating
- Verify ChurchSuite profile contains Name field
- Check Website URL is valid (must start with http/https)
- Ensure Email field is present in profile
- Review logs for profile fetch errors

### Duplicate Events
- Current logic checks Name + Date + User
- Clear old events if sync was interrupted
- Consider adding external event ID comparison

---

## Related Documentation

- ChurchSuite API Docs: https://churchsuite.com
- .NET HttpClient: https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient
- Entity Framework Core: https://learn.microsoft.com/en-us/ef/core/
- ASP.NET Core Logging: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging
