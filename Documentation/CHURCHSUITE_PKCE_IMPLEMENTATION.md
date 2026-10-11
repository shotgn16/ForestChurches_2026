# ChurchSuite PKCE Authentication Implementation

## Overview
This document summarizes the implementation of ChurchSuite OAuth PKCE (Proof Key for Code Exchange) authentication flow for the ForestChurches application. The implementation enables users to authenticate without requiring hardcoded client secrets, with fallback support for per-user ClientId configuration.

## Implementation Strategy

### Primary Flow: PKCE (RFC 7636)
- **No static secret required** - Uses code_challenge/code_verifier for public clients
- **Global ClientId only** - The configured ChurchSuite ClientId in Key Vault is used for all PKCE flows
- **User-friendly** - Users click "Sign In with ChurchSuite" and are taken directly to the ChurchSuite login page

### Secondary Flow: Per-User ClientId (Fallback)
- If PKCE fails or is unavailable, users can provide their own ClientId
- Per-user ClientId is stored in `ChurchAccount.ClientId` property
- Fallback happens automatically during token exchange

## Architecture

### New Core Services

#### 1. **IChurchSuiteTokenService** (Components\Http\ChurchSuiteTokenService.cs)
Centralized token lifecycle management service that:
- **GetValidAccessTokenAsync(userId)** - Returns a valid access token, refreshing if necessary
- **GetValidCredentialAsync(userId)** - Retrieves credential and ensures token is valid
- **HasValidTokenAsync(userId)** - Checks if token is still valid without refresh
- **RefreshTokenAsync(credential)** - Refreshes expired tokens automatically
- **StoreCredentialsAsync(...)** - Persists tokens to database after OAuth callback
- **UserHasAuthorizationAsync(userId)** - Checks if user has authorized with ChurchSuite

**Key Features:**
- Automatic token refresh when expired
- Database persistence in `ChurchSuiteCredential` table
- Support for both global and per-user ClientIds
- Logging for all operations

#### 2. **IChurchSuiteAuthenticationService** (Components\Http\ChurchSuiteAuthenticationService.cs)
PKCE orchestration service that:
- **GeneratePKCEAuthorizationUrlAsync(...)** - Creates authorization URL with code_challenge (S256)
- **GeneratePerUserClientIdAuthorizationUrlAsync(...)** - Creates URL for per-user ClientId flow
- **StorePKCEVerifierInSession(...)** - Saves code_verifier securely in session
- **GetPKCEVerifierFromSession(...)** - Retrieves code_verifier for token exchange
- **ClearPKCEDataFromSession(...)** - Cleans up session data after successful exchange

**Key Features:**
- PKCE S256 code_challenge generation
- Session-based verifier storage (secure, temporary)
- Unique state values for CSRF protection
- Support for redirect_uri customization

### Updated Controllers

#### 1. **ChurchsuiteController** (Controllers\ChurchsuiteController.cs)
Public API endpoints for UI integration:
- **GET /churchsuite/login-pkce** - Initiates PKCE flow
- **GET /churchsuite/authorize** - Initiates per-user ClientId flow
- **GET /churchsuite/check-auth** - Returns authorization status for UI

#### 2. **OAuthCallbackController** (Controllers\OAuthCallbackController.cs)
Handles OAuth callback:
- **POST /signin-churchsuite** - Receives authorization code from ChurchSuite
- Exchanges code for tokens using PKCE code_verifier
- Stores credentials via IChurchSuiteTokenService
- Creates external login session if user not authenticated
- Refreshes authenticated user's sign-in

### Updated Services

#### 1. **ChurchSuiteService** (Components\Http\ChurchSuiteService.cs)
Now uses centralized token service:
- **GetProfileFromOAuthAsync(ClaimsPrincipal)** - Uses token service to get valid token
- **GetProfileFromOAuthAsync(string)** - Existing method unchanged
- Automatic token refresh when fetching profile

#### 2. **ChurchSuiteEventSyncService** (Components\Http\ChurchSuiteEventSyncService.cs)
Syncs events using centralized token management:
- **SyncEventsAsync(userId)** - Uses token service to get valid credential
- Automatic refresh before API calls
- Returns `AUTHORIZE_REQUIRED` if user needs to authorize

### Updated UI

#### 1. **ExternalLogin.cshtml** (Areas\Identity\Pages\Account\ExternalLogin.cshtml)
- Updated ChurchSuite button to redirect to `/churchsuite/login-pkce`
- Supports return URL for post-login redirection

#### 2. **ChurchSuiteAuthStatus.cshtml** (Pages\Shared\ChurchSuiteAuthStatus.cshtml)
New Razor partial for displaying authorization status:
- Shows if user has authorized with ChurchSuite
- Shows if current token is valid
- Provides "Authorize" button to initiate PKCE flow
- Can be included on event sync pages or dashboard

### Database

Uses existing `ChurchSuiteCredential` table:
```csharp
public class ChurchSuiteCredential
{
	public int Id { get; set; }
	public string UserId { get; set; }
	public string Subdomain { get; set; }
	public string AccessToken { get; set; }
	public string RefreshToken { get; set; }
	public DateTime ExpiresAt { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public DateTime LastRefreshedAt { get; set; }
	public bool IsActive { get; set; }
	public string ClientId { get; set; } // Per-user ClientId if applicable
}
```

## Configuration

### Required Settings (from Key Vault / appsettings)
```json
{
  "ChurchSuiteOAuth": {
	"ClientId": "your-global-churchsuite-client-id",
	"ClientSecret": null, // Not needed for PKCE, optional for fallback
	"RedirectUri": "https://your-domain/signin-churchsuite",
	"AuthorizeEndpoint": "https://login.churchsuite.com/oauth2/authorize",
	"TokenEndpoint": "https://login.churchsuite.com/oauth2/token"
  }
}
```

### Session Configuration (Program.cs)
- Session storage configured with `AddDistributedMemoryCache()`
- Session timeout set appropriately
- Cookies configured with SameSite=Lax for OAuth callback

## User Flow

### Scenario 1: New User - PKCE Flow
1. User clicks "Sign In with ChurchSuite" on ExternalLogin page
2. App generates PKCE code_challenge and stores code_verifier in session
3. User redirected to ChurchSuite login page
4. User authenticates and authorizes the app
5. ChurchSuite redirects to `/signin-churchsuite?code=...&state=...`
6. App exchanges code for tokens using PKCE code_verifier
7. App fetches user profile from ChurchSuite API
8. If local user doesn't exist, creates external login session for registration
9. If local user exists, stores credentials and signs them in

### Scenario 2: Authenticated User - Credential Already Exists
1. User logged into ForestChurches
2. User clicks button to sync ChurchSuite events
3. App checks for existing valid token via `HasValidTokenAsync()`
4. If valid, uses it directly
5. If expired, automatically refreshes via `RefreshTokenAsync()`
6. If not authorized, redirects to PKCE login flow

### Scenario 3: Per-User ClientId Fallback
1. If global ClientId is not configured
2. App attempts to use user's `ChurchAccount.ClientId`
3. If neither available, user is prompted to authorize
4. Per-user ClientId is validated during token exchange

## Security Features

1. **PKCE Protection** - Uses RFC 7636 code_challenge/code_verifier
2. **State Parameter** - CSRF protection with unique state values
3. **Session Storage** - Code_verifier stored in server-side session (not URL)
4. **Token Expiration** - Automatic refresh before expiration
5. **SameSite Cookies** - Prevents cross-site request forgery
6. **Per-User Isolation** - Each user's tokens stored separately in database

## Testing Recommendations

### Manual Testing
1. **PKCE Flow**
   - Click "Sign In with ChurchSuite" on registration page
   - Verify redirected to ChurchSuite login
   - Authorize access
   - Verify redirected back and session created or user signed in

2. **Token Refresh**
   - Authorize with ChurchSuite
   - Wait for token to expire (or manually update database expiration)
   - Trigger event sync
   - Verify token refreshed automatically

3. **Per-User ClientId**
   - Disable global ClientId in appsettings
   - Provide user's own ClientId on profile page
   - Attempt to authorize
   - Verify fallback flow works

### Automated Testing
- Unit tests for `ChurchSuiteTokenService` (refresh logic, expiration checks)
- Unit tests for `ChurchSuiteAuthenticationService` (PKCE generation, session handling)
- Integration tests for OAuth callback flow
- Integration tests for event sync with token refresh

## Troubleshooting

### "No ClientId configured" Error
- Check Key Vault has `ChurchSuiteOAuth:ClientId` set
- Check appsettings.json has fallback value
- Check user's `ClientId` property if using per-user flow

### "PKCE code_verifier not found in session" Error
- Verify session is configured in Program.cs
- Check session timeout is reasonable (>5 minutes)
- Verify user completed OAuth flow within session lifetime

### Token Refresh Failures
- Verify refresh_token is stored in database
- Check ChurchSuite refresh_token endpoint is accessible
- Review logs for HTTP error details
- Ensure per-user ClientId matches the one used for initial authorization

### Event Sync Not Working
- Verify user has authorized (check `ChurchSuiteCredential` table)
- Check credentials are marked `IsActive = true`
- Verify subdomain is correct in credentials
- Check API access token is valid (refresh if needed)

## Files Modified/Created

### Created Files
- `Components\Http\ChurchSuiteTokenService.cs`
- `Components\Http\ChurchSuiteAuthenticationService.cs`
- `Pages\Shared\ChurchSuiteAuthStatus.cshtml`

### Modified Files
- `Controllers\ChurchsuiteController.cs` - Added new endpoints
- `Controllers\OAuthCallbackController.cs` - Refactored to use token service
- `Components\Http\ChurchSuiteService.cs` - Uses token service for token refresh
- `Components\Http\ChurchSuiteEventSyncService.cs` - Uses token service
- `Components\Configuration\ServiceRegistration.cs` - Registers new services
- `Areas\Identity\Pages\Account\ExternalLogin.cshtml` - Updated to use PKCE flow
- `Areas\Identity\Pages\Account\ExternalLogin.cshtml.cs` - Updated handler

### Unchanged But Related
- `Data\ForestChurchesContext.cs` - Uses existing `ChurchSuiteCredential` model
- `Models\ChurchSuiteCredential.cs` - Existing model, no changes

## Summary

The PKCE implementation provides a modern, secure OAuth flow that:
✅ **Eliminates the need for client secrets** in the primary flow
✅ **Improves user experience** with single-click sign-in
✅ **Provides fallback support** for per-user ClientId configuration
✅ **Centralizes token management** for consistency across the app
✅ **Automatically handles token refresh** transparently
✅ **Maintains backward compatibility** with existing event sync functionality
✅ **Follows OAuth 2.0 best practices** (PKCE, state parameter, secure storage)

All three requirements have been addressed:
1. ✅ PKCE-first authentication (no static client secret needed)
2. ✅ Per-user ClientId fallback option
3. ✅ Consistent token checking/refresh across all ChurchSuite features
