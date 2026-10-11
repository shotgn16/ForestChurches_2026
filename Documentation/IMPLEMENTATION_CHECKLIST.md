# ChurchSuite Integration - Implementation Checklist

## ✅ Completed Backend Implementation

### Service Layer
- [x] Enhanced `ChurchSuiteEventDto` with additional API fields (Venue, Url, Image, ImageThumb)
- [x] Implemented `IChurchSuiteService.GetCalendarEventsAsync()` method
- [x] Implemented `IChurchSuiteService.GetProfileAsync()` method
- [x] Implemented `IChurchSuiteService.MapChurchSuiteEventToEventsModel()` method
- [x] Added comprehensive logging to all methods (Info, Warning, Error levels)
- [x] Added exception handling and validation
- [x] Updated service to use ILogger dependency injection
- [x] Verified service registration in `ServiceRegistration.cs`

### Integration Points
- [x] Updated `Register.cshtml.cs` to accept ChurchSuite credentials in InputModel
- [x] Added profile fetch after successful registration
- [x] Implemented safe URI parsing for Website field
- [x] Added non-fatal error handling (registration continues on ChurchSuite failure)
- [x] Updated `Events\Index.cshtml.cs` with `OnPostSyncFromChurchSuiteAsync()` method
- [x] Implemented event duplicate detection (Name + Date + User)
- [x] Updated `ChurchProfile.cshtml.cs` with `OnPostSyncChurchSuiteProfileAsync()` method
- [x] Implemented selective profile update (Name, Website, Email)
- [x] Added user update for email synchronization

### Build Status
- [x] All code compiles successfully
- [x] No compilation errors
- [x] Dependencies properly injected
- [x] Service registration verified

---

## ⏳ Next Steps - UI Implementation (Not Yet Done)

### 1. Register Page UI
File: `Areas\Identity\Pages\Account\Register.cshtml`

Add optional ChurchSuite credentials section:
```html
<!-- Optional ChurchSuite Integration -->
<fieldset>
	<legend>ChurchSuite Integration (Optional)</legend>
	<div class="form-group">
		<label asp-for="Input.ChurchSuiteAccountId"></label>
		<input asp-for="Input.ChurchSuiteAccountId" class="form-control" 
			   placeholder="Leave blank to skip" />
		<small class="form-text text-muted">
			Your ChurchSuite Account ID for profile auto-population
		</small>
		<span asp-validation-for="Input.ChurchSuiteAccountId" class="text-danger"></span>
	</div>
	<div class="form-group">
		<label asp-for="Input.ChurchSuiteApiKey"></label>
		<input asp-for="Input.ChurchSuiteApiKey" class="form-control" type="password"
			   placeholder="Leave blank to skip" />
		<small class="form-text text-muted">
			Your ChurchSuite API Key (kept secure, never stored)
		</small>
		<span asp-validation-for="Input.ChurchSuiteApiKey" class="text-danger"></span>
	</div>
</fieldset>
```

### 2. Events Page UI
File: `Pages\Events\Index.cshtml`

Add import section:
```html
@section ImportEvents
{
	<div class="card mb-4">
		<div class="card-header bg-primary text-white">
			<h5 class="mb-0">
				<i class="fas fa-church"></i> Import Events from ChurchSuite
			</h5>
		</div>
		<div class="card-body">
			<form method="post" asp-page-handler="SyncFromChurchSuite" 
				  class="needs-validation" novalidate>
				<div class="row">
					<div class="col-md-6">
						<div class="form-group mb-3">
							<label for="churchSuiteAccountId" class="form-label">
								ChurchSuite Account ID
							</label>
							<input type="text" class="form-control" id="churchSuiteAccountId" 
								   name="churchSuiteAccountId" required />
							<div class="invalid-feedback">
								Please provide your ChurchSuite Account ID.
							</div>
						</div>
					</div>
					<div class="col-md-6">
						<div class="form-group mb-3">
							<label for="churchSuiteApiKey" class="form-label">
								ChurchSuite API Key
							</label>
							<input type="password" class="form-control" id="churchSuiteApiKey" 
								   name="churchSuiteApiKey" required />
							<div class="invalid-feedback">
								Please provide your ChurchSuite API Key.
							</div>
						</div>
					</div>
				</div>
				<button type="submit" class="btn btn-primary">
					<i class="fas fa-sync-alt"></i> Sync Events from ChurchSuite
				</button>
			</form>
		</div>
	</div>
}

<!-- Display Status Message -->
@if (!string.IsNullOrEmpty(Model.StatusMessage))
{
	<div class="alert alert-info alert-dismissible fade show" role="alert">
		<strong>Info:</strong> @Model.StatusMessage
		<button type="button" class="btn-close" data-bs-dismiss="alert" 
				aria-label="Close"></button>
	</div>
}
```

### 3. Church Profile UI
File: `Areas\Identity\Pages\Account\Manage\ChurchProfile.cshtml`

Add refresh profile section:
```html
@section SyncProfile
{
	<div class="card mb-4">
		<div class="card-header bg-info text-white">
			<h5 class="mb-0">
				<i class="fas fa-redo"></i> Refresh Profile from ChurchSuite
			</h5>
		</div>
		<div class="card-body">
			<p class="text-muted">
				Update your church information directly from your ChurchSuite account.
			</p>
			<form method="post" asp-page-handler="SyncChurchSuiteProfile" 
				  class="needs-validation" novalidate>
				<div class="row">
					<div class="col-md-6">
						<div class="form-group mb-3">
							<label for="churchSuiteAccountId" class="form-label">
								ChurchSuite Account ID
							</label>
							<input type="text" class="form-control" id="churchSuiteAccountId" 
								   name="churchSuiteAccountId" required />
							<div class="invalid-feedback">
								Please provide your ChurchSuite Account ID.
							</div>
						</div>
					</div>
					<div class="col-md-6">
						<div class="form-group mb-3">
							<label for="churchSuiteApiKey" class="form-label">
								ChurchSuite API Key
							</label>
							<input type="password" class="form-control" id="churchSuiteApiKey" 
								   name="churchSuiteApiKey" required />
							<div class="invalid-feedback">
								Please provide your ChurchSuite API Key.
							</div>
						</div>
					</div>
				</div>
				<button type="submit" class="btn btn-info">
					<i class="fas fa-cloud-download-alt"></i> Refresh from ChurchSuite
				</button>
			</form>
		</div>
	</div>
}

<!-- Display Status Message -->
@if (!string.IsNullOrEmpty(Model.StatusMessage))
{
	<div class="alert alert-info alert-dismissible fade show" role="alert">
		<strong>Update:</strong> @Model.StatusMessage
		<button type="button" class="btn-close" data-bs-dismiss="alert" 
				aria-label="Close"></button>
	</div>
}
```

---

## 📋 Testing Checklist

### Functional Tests
- [ ] **Registration Flow**
  - Register with ChurchSuite credentials
  - Verify profile auto-populates from ChurchSuite
  - Register without credentials
  - Register with invalid credentials (should fail gracefully)

- [ ] **Event Sync**
  - Sync events from ChurchSuite via Events page
  - Verify events appear correctly in list
  - Re-run sync, verify duplicates are skipped
  - Check event details (title, date, time, location)

- [ ] **Profile Refresh**
  - Manually refresh profile on Church Profile page
  - Verify Name, Website, Email update correctly
  - Update ChurchSuite profile, refresh again
  - Verify changes are reflected

### Edge Cases
- [ ] Empty or null fields from ChurchSuite
- [ ] Events with missing datetime values
- [ ] Invalid URI in website field
- [ ] Extremely long event descriptions
- [ ] Special characters in event names

### Error Scenarios
- [ ] Invalid ChurchSuite credentials
- [ ] Expired API key
- [ ] ChurchSuite service unavailable
- [ ] Network timeout during sync
- [ ] Invalid JSON response

### Performance
- [ ] Sync with 100+ events (measure time)
- [ ] Check database query performance
- [ ] Monitor logging output for excessive verbose logs

---

## 🔐 Security Considerations

- [ ] API keys are never logged
- [ ] Credentials validated before API calls
- [ ] HTTPS enforced for all ChurchSuite API calls
- [ ] Consider encrypting stored credentials if saved
- [ ] Validate and sanitize all API response data
- [ ] Rate limiting on API calls (if needed)

---

## 📝 Documentation

- [x] Created `CHURCHSUITE_INTEGRATION_GUIDE.md` with:
  - Architecture overview
  - Component descriptions
  - Integration point explanations
  - Event mapping details
  - Error handling strategies
  - UI integration guidance
  - Testing checklist
  - Troubleshooting guide
  - Future enhancement suggestions

---

## 🚀 Deployment Checklist

Before deploying to production:
- [ ] All tests pass
- [ ] Code reviewed for security issues
- [ ] Logging configured appropriately (not too verbose)
- [ ] UI sections added to both register and profile pages
- [ ] Documentation provided to end users
- [ ] Support team trained on ChurchSuite integration
- [ ] Monitoring/alerts set up for API failures
- [ ] Backup/rollback plan documented

---

## 📞 Support Resources

### For Users
- Explain how to find ChurchSuite Account ID and API Key
- Document step-by-step registration flow with ChurchSuite
- Guide for event import and profile refresh
- Troubleshooting common issues

### For Developers
- Code comments explain key methods
- Logging provides detailed diagnostic information
- Error messages indicate likely causes
- Exception types help with debugging

---

**Last Updated:** Implementation completed and build verified
**Status:** Backend complete, ready for UI implementation and testing
