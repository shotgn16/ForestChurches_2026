# Ready-to-Use Code Snippets

This document contains UI code snippets ready to paste into your Razor views.

---

## 1. Register.cshtml - Add ChurchSuite Credentials Section

**Location:** `Areas\Identity\Pages\Account\Register.cshtml`

**Add this section inside the form, after password fields but before the register button:**

```html
<!-- ChurchSuite Integration Section -->
<div class="card mb-4">
	<div class="card-header bg-light">
		<h6 class="mb-0">
			<i class="fas fa-church"></i> ChurchSuite Integration (Optional)
		</h6>
		<small class="text-muted">
			Automatically populate your church profile from ChurchSuite
		</small>
	</div>
	<div class="card-body">
		<p class="text-muted small">
			If you have a ChurchSuite account, you can provide your credentials here to automatically 
			import your church information. This is optional - you can register without it.
		</p>

		<div class="row">
			<div class="col-md-6">
				<div class="form-group mb-3">
					<label asp-for="Input.ChurchSuiteAccountId" class="form-label">
						ChurchSuite Account ID
					</label>
					<input asp-for="Input.ChurchSuiteAccountId" 
						   type="text" 
						   class="form-control" 
						   placeholder="e.g., abc123 (from your ChurchSuite URL)" />
					<small class="form-text text-muted d-block mt-1">
						Found in your ChurchSuite dashboard URL or settings
					</small>
				</div>
			</div>

			<div class="col-md-6">
				<div class="form-group mb-3">
					<label asp-for="Input.ChurchSuiteApiKey" class="form-label">
						ChurchSuite API Key
					</label>
					<input asp-for="Input.ChurchSuiteApiKey" 
						   type="password" 
						   class="form-control" 
						   placeholder="Your ChurchSuite API key" />
					<small class="form-text text-muted d-block mt-1">
						From Settings → Integrations → API in ChurchSuite
					</small>
				</div>
			</div>
		</div>

		<div class="alert alert-info" role="alert">
			<i class="fas fa-info-circle"></i>
			<strong>Privacy Notice:</strong> Your credentials are used only to fetch your profile 
			information and are never stored on our servers.
		</div>
	</div>
</div>
```

---

## 2. Events/Index.cshtml - Add Event Import Section

**Location:** `Pages\Events\Index.cshtml`

**Add this section at the top of the page, before the events list:**

```html
@* ChurchSuite Event Import Section *@
<div class="row mb-4">
	<div class="col-12">
		<div class="card">
			<div class="card-header bg-primary text-white">
				<div class="d-flex justify-content-between align-items-center">
					<h5 class="mb-0">
						<i class="fas fa-calendar-alt"></i> Import Events from ChurchSuite
					</h5>
				</div>
			</div>
			<div class="card-body">
				@if (!string.IsNullOrEmpty(Model.StatusMessage))
				{
					@if (Model.StatusMessage.Contains("Error"))
					{
						<div class="alert alert-danger alert-dismissible fade show" role="alert">
							<i class="fas fa-exclamation-circle"></i> <strong>Error:</strong> @Model.StatusMessage
							<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
						</div>
					}
					else if (Model.StatusMessage.Contains("Warning"))
					{
						<div class="alert alert-warning alert-dismissible fade show" role="alert">
							<i class="fas fa-exclamation-triangle"></i> <strong>Warning:</strong> @Model.StatusMessage
							<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
						</div>
					}
					else
					{
						<div class="alert alert-success alert-dismissible fade show" role="alert">
							<i class="fas fa-check-circle"></i> <strong>Success:</strong> @Model.StatusMessage
							<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
						</div>
					}
				}

				<p class="text-muted mb-3">
					Import your church's events directly from ChurchSuite. Events that already exist 
					in your calendar will not be duplicated.
				</p>

				<form method="post" asp-page-handler="SyncFromChurchSuite" class="needs-validation" novalidate>
					<div class="row align-items-end">
						<div class="col-md-5">
							<div class="form-group">
								<label for="churchSuiteAccountId" class="form-label">
									<i class="fas fa-building"></i> ChurchSuite Account ID
								</label>
								<input type="text" 
									   class="form-control" 
									   id="churchSuiteAccountId" 
									   name="churchSuiteAccountId" 
									   placeholder="e.g., abc123"
									   required />
								<div class="invalid-feedback">
									Please provide your ChurchSuite Account ID.
								</div>
							</div>
						</div>

						<div class="col-md-5">
							<div class="form-group">
								<label for="churchSuiteApiKey" class="form-label">
									<i class="fas fa-key"></i> ChurchSuite API Key
								</label>
								<input type="password" 
									   class="form-control" 
									   id="churchSuiteApiKey" 
									   name="churchSuiteApiKey" 
									   placeholder="Your API key"
									   required />
								<div class="invalid-feedback">
									Please provide your ChurchSuite API Key.
								</div>
							</div>
						</div>

						<div class="col-md-2">
							<button type="submit" class="btn btn-primary w-100">
								<i class="fas fa-sync-alt"></i> Sync Events
							</button>
						</div>
					</div>
				</form>

				<hr class="my-3" />

				<p class="small text-muted mb-0">
					<i class="fas fa-shield-alt"></i>
					<strong>Secure:</strong> Your credentials are used only for this sync and are not stored.
				</p>
			</div>
		</div>
	</div>
</div>
```

---

## 3. ChurchProfile.cshtml - Add Manual Profile Sync Section

**Location:** `Areas\Identity\Pages\Account\Manage\ChurchProfile.cshtml`

**Add this section in an appropriate location on the profile page, perhaps after basic church info:**

```html
@* ChurchSuite Profile Sync Section *@
<div class="row mb-4">
	<div class="col-12">
		<div class="card">
			<div class="card-header bg-info text-white">
				<div class="d-flex justify-content-between align-items-center">
					<h5 class="mb-0">
						<i class="fas fa-cloud-download-alt"></i> Sync Profile from ChurchSuite
					</h5>
				</div>
			</div>
			<div class="card-body">
				@if (!string.IsNullOrEmpty(Model.StatusMessage))
				{
					@if (Model.StatusMessage.Contains("Error"))
					{
						<div class="alert alert-danger alert-dismissible fade show" role="alert">
							<i class="fas fa-exclamation-circle"></i> <strong>Error:</strong> @Model.StatusMessage
							<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
						</div>
					}
					else if (Model.StatusMessage.Contains("Warning"))
					{
						<div class="alert alert-warning alert-dismissible fade show" role="alert">
							<i class="fas fa-exclamation-triangle"></i> <strong>Warning:</strong> @Model.StatusMessage
							<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
						</div>
					}
					else
					{
						<div class="alert alert-success alert-dismissible fade show" role="alert">
							<i class="fas fa-check-circle"></i> <strong>Success:</strong> @Model.StatusMessage
							<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Close"></button>
						</div>
					}
				}

				<p class="text-muted mb-3">
					Update your church name, website, and email directly from your ChurchSuite account. 
					This will overwrite the corresponding fields below.
				</p>

				<form method="post" asp-page-handler="SyncChurchSuiteProfile" class="needs-validation" novalidate>
					<div class="row align-items-end">
						<div class="col-md-5">
							<div class="form-group">
								<label for="churchSuiteAccountId" class="form-label">
									<i class="fas fa-building"></i> ChurchSuite Account ID
								</label>
								<input type="text" 
									   class="form-control" 
									   id="churchSuiteAccountId" 
									   name="churchSuiteAccountId" 
									   placeholder="e.g., abc123"
									   required />
								<div class="invalid-feedback">
									Please provide your ChurchSuite Account ID.
								</div>
							</div>
						</div>

						<div class="col-md-5">
							<div class="form-group">
								<label for="churchSuiteApiKey" class="form-label">
									<i class="fas fa-key"></i> ChurchSuite API Key
								</label>
								<input type="password" 
									   class="form-control" 
									   id="churchSuiteApiKey" 
									   name="churchSuiteApiKey" 
									   placeholder="Your API key"
									   required />
								<div class="invalid-feedback">
									Please provide your ChurchSuite API Key.
								</div>
							</div>
						</div>

						<div class="col-md-2">
							<button type="submit" class="btn btn-info w-100">
								<i class="fas fa-redo"></i> Refresh
							</button>
						</div>
					</div>

					<div class="alert alert-warning mt-3" role="alert">
						<i class="fas fa-info-circle"></i>
						<strong>Note:</strong> This will update:
						<ul class="mb-0 mt-2">
							<li>Church Name</li>
							<li>Website URL</li>
							<li>Your email address (if different from ChurchSuite)</li>
						</ul>
					</div>
				</form>

				<hr class="my-3" />

				<p class="small text-muted mb-0">
					<i class="fas fa-shield-alt"></i>
					<strong>Secure:</strong> Your credentials are used only for this sync and are not stored.
				</p>
			</div>
		</div>
	</div>
</div>
```

---

## 4. Bootstrap Validation JavaScript (Optional)

**Add to your layout or page to enable Bootstrap form validation:**

```html
<script>
	// Enable Bootstrap form validation
	(function() {
		'use strict';
		window.addEventListener('load', function() {
			var forms = document.querySelectorAll('.needs-validation');
			var validation = Array.prototype.filter.call(forms, function(form) {
				form.addEventListener('submit', function(event) {
					if (form.checkValidity() === false) {
						event.preventDefault();
						event.stopPropagation();
					}
					form.classList.add('was-validated');
				}, false);
			});
		}, false);
	})();
</script>
```

---

## 5. CSS Enhancement (Optional)

**Add to your CSS file to style the ChurchSuite sections:**

```css
/* ChurchSuite Integration Styling */
.churchsuite-section {
	border-left: 4px solid #0d6efd;
	padding-left: 1rem;
}

.churchsuite-icon {
	color: #0d6efd;
	margin-right: 0.5rem;
}

.card-header.churchsuite-header {
	background: linear-gradient(135deg, #0d6efd 0%, #0854ca 100%);
}

.sync-status-badge {
	font-size: 0.85rem;
	padding: 0.4rem 0.8rem;
}

.sync-info-alert {
	border-left: 4px solid #0d6efd;
}
```

---

## 6. Helper Text/Documentation for Users

**Display on your help/FAQ page:**

```html
<div class="accordion" id="churchsuiteHelp">
	<div class="accordion-item">
		<h2 class="accordion-header">
			<button class="accordion-button" type="button" data-bs-toggle="collapse" 
					data-bs-target="#findAccountId">
				<i class="fas fa-question-circle"></i> How do I find my ChurchSuite Account ID?
			</button>
		</h2>
		<div id="findAccountId" class="accordion-collapse collapse" 
			 data-bs-parent="#churchsuiteHelp">
			<div class="accordion-body">
				<ol>
					<li>Log in to your ChurchSuite account</li>
					<li>Look at the URL in your browser - it will be something like: 
						<code>https://abc123.churchsuite.com/dashboard</code>
					</li>
					<li>Your Account ID is the part before .churchsuite.com - in this example: <code>abc123</code></li>
					<li>You can also find it in Settings → Account Information</li>
				</ol>
			</div>
		</div>
	</div>

	<div class="accordion-item">
		<h2 class="accordion-header">
			<button class="accordion-button" type="button" data-bs-toggle="collapse" 
					data-bs-target="#findApiKey">
				<i class="fas fa-question-circle"></i> How do I find my ChurchSuite API Key?
			</button>
		</h2>
		<div id="findApiKey" class="accordion-collapse collapse" 
			 data-bs-parent="#churchsuiteHelp">
			<div class="accordion-body">
				<ol>
					<li>Log in to your ChurchSuite account with admin access</li>
					<li>Go to <strong>Settings</strong> → <strong>Integrations</strong></li>
					<li>Click on <strong>API</strong></li>
					<li>You'll see your API Key displayed. Click to copy it.</li>
					<li>If you don't see an API Key, click "Generate New API Key"</li>
				</ol>
				<p class="alert alert-warning">
					<strong>⚠️ Security Note:</strong> Your API key is like a password. 
					Never share it with anyone. We don't store it - it's only used for this sync.
				</p>
			</div>
		</div>
	</div>

	<div class="accordion-item">
		<h2 class="accordion-header">
			<button class="accordion-button" type="button" data-bs-toggle="collapse" 
					data-bs-target="#whatSyncs">
				<i class="fas fa-question-circle"></i> What information gets synced?
			</button>
		</h2>
		<div id="whatSyncs" class="accordion-collapse collapse" 
			 data-bs-parent="#churchsuiteHelp">
			<div class="accordion-body">
				<h6>From ChurchSuite Profile:</h6>
				<ul>
					<li>Church Name</li>
					<li>Website URL</li>
					<li>Email Address</li>
				</ul>
				<h6>From ChurchSuite Calendar:</h6>
				<ul>
					<li>Event Title</li>
					<li>Description</li>
					<li>Date and Time</li>
					<li>Location</li>
					<li>Event URL (if available)</li>
				</ul>
			</div>
		</div>
	</div>

	<div class="accordion-item">
		<h2 class="accordion-header">
			<button class="accordion-button" type="button" data-bs-toggle="collapse" 
					data-bs-target="#safeSecurity">
				<i class="fas fa-question-circle"></i> Is it safe to share my ChurchSuite credentials?
			</button>
		</h2>
		<div id="safeSecurity" class="accordion-collapse collapse" 
			 data-bs-parent="#churchsuiteHelp">
			<div class="accordion-body">
				<p>Yes, with these precautions:</p>
				<ul>
					<li>Your credentials are only used to fetch your profile and events</li>
					<li>We <strong>never store</strong> your API key on our servers</li>
					<li>The connection to ChurchSuite is always encrypted (HTTPS)</li>
					<li>You can revoke the API key in ChurchSuite at any time</li>
				</ul>
				<p class="alert alert-info">
					<i class="fas fa-lock"></i> If you ever feel unsafe, you can regenerate 
					your API key in ChurchSuite, and the old one will stop working.
				</p>
			</div>
		</div>
	</div>
</div>
```

---

## Implementation Instructions

### Step 1: Add Register.cshtml Section
1. Open `Areas\Identity\Pages\Account\Register.cshtml`
2. Find the closing `</form>` tag
3. Add the first snippet just before that tag
4. Save the file

### Step 2: Add Events Page Section
1. Open `Pages\Events\Index.cshtml`
2. Find where you display the events list
3. Add the second snippet before the events list
4. Save the file

### Step 3: Add ChurchProfile Page Section
1. Open `Areas\Identity\Pages\Account\Manage\ChurchProfile.cshtml`
2. Find an appropriate location (perhaps after the church name field)
3. Add the third snippet
4. Save the file

### Step 4: Add Validation Script
1. Open your `_Layout.cshtml` or page footer
2. Add the validation script before closing `</body>` tag
3. Save the file

### Step 5: Test
1. Build the solution
2. Run the application
3. Test Registration with ChurchSuite credentials
4. Test Event sync on Events page
5. Test Profile sync on ChurchProfile page

---

**All code is ready to use and follows Bootstrap 5 and ASP.NET Core conventions.**
