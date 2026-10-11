# ChurchSuite OAuth Implementation Guide

## Overview
This document describes the complete ChurchSuite OAuth integration implemented in ForestChurches. Users can now login with a single "Login with ChurchSuite" button that redirects them to ChurchSuite for authentication, after which their profile is automatically synced.

## Architecture

### Components Implemented

#### 1. **OAuth Configuration (Program.cs)**
- Registered ChurchSuite OAuth provider with endpoints:
  - Authorization: `https://oauth.churchsuite.com/oauth/authorize`
  - Token: `https://oauth.churchsuite.com/oauth/token`
  - User Info: `https://oauth.churchsuite.com/oauth/me`
- Callback path: `/signin-churchsuite`
- Token saving enabled for profile syncing
- Scopes: `profile:read`, `account:read`

#### 2. **ChurchSuiteService Enhancement (Components/Http/ChurchSuiteService.cs)**
- Added `GetProfileFromOAuthAsync(ClaimsPrincipal user)` method
  - Extracts OAuth access token from user claims
  - Fetches profile from ChurchSuite OAuth endpoint
  - Returns ChurchSuiteProfileResponse with account details

#### 3. **External Login Callback (Areas/Identity/Pages/Account/ExternalLogin.cshtml.cs)**
- Enhanced to detect ChurchSuite OAuth login
- Automatic profile sync on successful login:
  - Existing users: Profile data updated on each login
  - New users: Profile data synced during account creation
- **SyncChurchSuiteProfileAsync** method:
  - Fetches profile from ChurchSuite using OAuth token
  - Updates ChurchInformation (Name, Website)
  - Updates ChurchAccount (Email)
  - Creates ChurchInformation if not exists
  - Non-fatal: Login succeeds even if sync fails

#### 4. **Church Profile Page (Areas/Identity/Pages/Account/Manage/ChurchProfile.cshtml)**
- Replaced manual API key form with OAuth sync
- New "Refresh from ChurchSuite" button
- Users can re-sync their profile data at any time
- No CSS changes - uses inline styling

#### 5. **Church Profile Handler (Areas/Identity/Pages/Account/Manage/ChurchProfile.cshtml.cs)**
- Added `OnPostSyncChurchSuiteOAuthAsync` handler
- Fetches latest profile from ChurchSuite
- Updates church information and user email
- Displays success/error messages to user

#### 6. **Login Page Enhancement (Areas/Identity/Pages/Account/Login.cshtml)**
- Added "Login with ChurchSuite" button
- Inline styling for clean integration
- Routes to ExternalLogin with provider=ChurchSuite
- No CSS changes

## Testing Procedure

### Prerequisites
1. **Key Vault Configuration Required** - Add these secrets:
   - `churchsuite-oauth-client-id`: Your ChurchSuite OAuth app Client ID
   - `churchsuite-oauth-client-secret`: Your ChurchSuite OAuth app Client Secret
   - `churchsuite-oauth-callback-url`: Full callback URL (e.g., `https://yourapp.com/signin-churchsuite`)

2. **ChurchSuite OAuth App Setup**:
   - Create OAuth application in ChurchSuite admin panel
   - Set redirect URI to: `https://yourdomain.com/signin-churchsuite`
   - Note the Client ID and Client Secret

### Test Scenarios

#### Scenario 1: New User Registration via ChurchSuite OAuth
1. Navigate to Login page
2. Click "Login with ChurchSuite" button
3. Redirected to ChurchSuite OAuth login
4. After authentication, redirected back to ForestChurches
5. **Expected Result**:
   - User account created
   - ExternalLogin confirmation page shown (or auto-signed in if configured)
   - ChurchInformation table populated with:
	 - Church Name from ChurchSuite
	 - Website from ChurchSuite
	 - User Email from ChurchSuite
   - Redirect to home page

#### Scenario 2: Existing User Login via ChurchSuite OAuth
1. User with existing ChurchSuite external login attempts login
2. Click "Login with ChurchSuite" button
3. ChurchSuite OAuth completes
4. **Expected Result**:
   - User signed in immediately
   - Church profile automatically synced (Name, Website, Email)
   - Redirect to ReturnUrl or home page
   - Status message shows successful sync

#### Scenario 3: Manual Profile Refresh
1. Logged-in user navigates to Church Profile page
2. Scrolls to "Sync Profile from ChurchSuite" section
3. Clicks "Refresh from ChurchSuite" button
4. **Expected Result**:
   - Profile data fetched from ChurchSuite
   - Church name updated
   - Website updated
   - Email updated
   - Status message confirms success
   - Page reloads with updated data

#### Scenario 4: Error Handling
1. User attempts OAuth login but ChurchSuite OAuth fails
2. **Expected Result**:
   - Error message displayed
   - User remains on ExternalLogin confirmation page
   - Can try again or use email/password login

2. Manual sync attempted with invalid/expired OAuth token
3. **Expected Result**:
   - Error message: "Could not sync profile from ChurchSuite"
   - Page remains on Church Profile
   - Can try again or update profile manually

### Verification Checklist

- [ ] Login page displays "Login with ChurchSuite" button
- [ ] Clicking button redirects to ChurchSuite OAuth
- [ ] Successfully authenticated users are redirected back with auth code
- [ ] New users can complete registration with ChurchSuite data
- [ ] Existing users are automatically signed in
- [ ] Church profile syncs on login (check ChurchInformation table)
- [ ] Church profile syncs on manual refresh (check ChurchInformation table)
- [ ] Status messages display correctly
- [ ] Profile updates appear in Church Profile form
- [ ] Email updates work correctly
- [ ] Error messages are clear and helpful
- [ ] No CSS styling is broken

## Database Changes

No migrations required. Existing ChurchInformation and ChurchAccount tables are used.

## Future Enhancements

1. **Token Refresh**: Implement refresh token flow to update OAuth tokens automatically
2. **Event Sync**: Sync ChurchSuite calendar events using OAuth token
3. **Multi-Account**: Allow users to link multiple ChurchSuite accounts
4. **Scope Expansion**: Request additional ChurchSuite scopes if needed:
   - `contacts:read` - For member data
   - `calendars:read` - For event data
   - `announcements:read` - For announcements

## Troubleshooting

### Issue: "OAuth provider not configured"
- **Cause**: Key Vault secrets not set
- **Solution**: Add the three required secrets to Key Vault

### Issue: "Redirect URI mismatch"
- **Cause**: Callback URL in ChurchSuite OAuth app doesn't match
- **Solution**: Ensure `churchsuite-oauth-callback-url` in Key Vault matches exactly what's configured in ChurchSuite

### Issue: OAuth login redirects but doesn't sign in
- **Cause**: OAuth endpoint URLs incorrect or scopes insufficient
- **Solution**: Verify ChurchSuite OAuth endpoint documentation

### Issue: Profile sync fails but login succeeds
- **Cause**: ChurchSuite OAuth endpoint returns unexpected format
- **Solution**: Check logs for profile fetch errors; manually update profile

## Code Files Modified

1. **Program.cs** - OAuth provider registration
2. **Components/Http/ChurchSuiteService.cs** - OAuth profile fetching
3. **Areas/Identity/Pages/Account/ExternalLogin.cshtml.cs** - Profile sync on login
4. **Areas/Identity/Pages/Account/ExternalLogin.cshtml** - New button (inline)
5. **Areas/Identity/Pages/Account/Manage/ChurchProfile.cshtml** - Replaced manual form
6. **Areas/Identity/Pages/Account/Manage/ChurchProfile.cshtml.cs** - OAuth sync handler

## Security Considerations

- OAuth tokens are stored securely in user claims by ASP.NET Core Identity
- Access tokens used only within the application for profile fetching
- No API keys stored in database (unlike previous implementation)
- CSRF protection on all forms
- HTTPS required for OAuth callbacks
- Data Protection configured for token encryption

## Next Steps

1. Configure Key Vault secrets with ChurchSuite OAuth credentials
2. Test all scenarios listed above
3. Monitor application logs for any OAuth-related errors
4. Gather user feedback on UX
5. Plan for additional ChurchSuite API integrations if needed
