# ChurchSuite OAuth Callback URL - Complete Setup Guide

## Quick Start

The ChurchSuite OAuth callback URL implementation is now complete. Here's what was set up:

### What Was Implemented

1. **OAuth Callback Controller** (`Controllers/OAuthCallbackController.cs`)
   - Handles `/signin-churchsuite` endpoint
   - Receives OAuth callbacks from ChurchSuite
   - Debug endpoint at `/oauth-debug` for troubleshooting

2. **Configuration Helper** (`Components/Configuration/ChurchSuiteOAuthConfiguration.cs`)
   - Manages OAuth secrets from multiple sources
   - Fallback priority: Environment Variables > Key Vault > appsettings
   - Validates configuration on startup

3. **Program.cs OAuth Setup** (lines 264-315)
   - Configured ChurchSuite OAuth provider
   - Added OAuth event handlers for better control
   - Proper error handling and logging

4. **Documentation**
   - `OAUTH_CALLBACK_CONFIGURATION.md` - Technical reference
   - This file - Complete setup guide

## Three-Step Setup

### Step 1: Create ChurchSuite OAuth App

1. Log into your ChurchSuite admin panel
2. Go to **Settings → Integrations → OAuth**
3. Create a new OAuth application
4. Fill in:
   - **App Name:** ForestChurches
   - **Redirect URIs:** (Add your callback URL - see Step 2)
5. Save and note the **Client ID** and **Client Secret**

### Step 2: Determine Your Callback URL

Choose the appropriate callback URL for your environment:

**Local Development:**
```
https://localhost:7001/signin-churchsuite
```

**Staging/Testing:**
```
https://staging.forestchurches.co.uk/signin-churchsuite
```

**Production:**
```
https://forestchurches.co.uk/signin-churchsuite
```
or
```
https://www.forestchurches.co.uk/signin-churchsuite
```

> **Important:** The callback URL must:
> - Be HTTPS (except localhost)
> - Be an absolute URL (include scheme and domain)
> - End with `/signin-churchsuite`
> - Match exactly in both ChurchSuite and Key Vault

### Step 3: Store Credentials

Choose your configuration method:

#### Option A: Azure Key Vault (Recommended - Production)

```bash
# Set Client ID
az keyvault secret set \
  --vault-name "your-vault-name" \
  --name "churchsuite-oauth-client-id" \
  --value "your-client-id-from-churchsuite"

# Set Client Secret
az keyvault secret set \
  --vault-name "your-vault-name" \
  --name "churchsuite-oauth-client-secret" \
  --value "your-client-secret-from-churchsuite"

# Set Callback URL
az keyvault secret set \
  --vault-name "your-vault-name" \
  --name "churchsuite-oauth-callback-url" \
  --value "https://your-domain.com/signin-churchsuite"
```

#### Option B: Environment Variables (Development)

```bash
# Windows (PowerShell)
$env:CHURCHSUITE_OAUTH_CLIENT_ID = "your-client-id"
$env:CHURCHSUITE_OAUTH_CLIENT_SECRET = "your-client-secret"
$env:CHURCHSUITE_OAUTH_CALLBACK_URL = "https://localhost:7001/signin-churchsuite"

# Linux/Mac
export CHURCHSUITE_OAUTH_CLIENT_ID="your-client-id"
export CHURCHSUITE_OAUTH_CLIENT_SECRET="your-client-secret"
export CHURCHSUITE_OAUTH_CALLBACK_URL="https://localhost:7001/signin-churchsuite"
```

#### Option C: appsettings.Development.json (Development Only)

Copy `appsettings.ChurchSuiteOAuth.example.json` to `appsettings.Development.json`:

```json
{
  "ChurchSuite": {
	"OAuth": {
	  "ClientId": "your-client-id",
	  "ClientSecret": "your-client-secret",
	  "CallbackUrl": "https://localhost:7001/signin-churchsuite"
	}
  }
}
```

## Testing the Setup

### 1. Start Your Application

```bash
dotnet run
```

### 2. Access the Debug Page

Visit: `https://localhost:7001/oauth-debug`

You should see:
- Callback URL being used
- Current authentication status
- Expected: "Not authenticated"

### 3. Test OAuth Login

1. Go to the login page: `https://localhost:7001/Identity/Account/Login`
2. Click "🔐 Login with ChurchSuite"
3. You should be redirected to ChurchSuite OAuth
4. Enter your ChurchSuite credentials
5. You should be redirected back to `/signin-churchsuite`
6. You should be automatically signed in
7. You should see your church profile synced

### 4. Verify Profile Sync

1. Go to Church Profile page: `/Identity/Account/Manage/ChurchProfile`
2. Check that the following are populated from ChurchSuite:
   - Church Name
   - Website URL
   - Email address

## Troubleshooting

### Error: "Invalid Client"

**Cause:** Client ID or Secret is wrong

**Fix:**
1. Verify values in ChurchSuite admin panel
2. Copy exactly (watch for spaces)
3. Regenerate if needed in ChurchSuite

### Error: "Redirect URI Mismatch"

**Cause:** Callback URL doesn't match between ChurchSuite and ForestChurches

**Fix:**
1. Check ChurchSuite OAuth app settings
2. Check Key Vault secrets
3. Must match exactly:
   - Protocol (https://)
   - Domain (including www if needed)
   - Port (7001 for localhost)
   - Path (/signin-churchsuite)

Example mismatches:
```
❌ https://localhost:7001/signin-churchsuite (registered)
✅ https://localhost:7001/signin-churchsuite (used)

❌ https://forestchurches.co.uk/signin-churchsuite (registered)
✅ https://www.forestchurches.co.uk/signin-churchsuite (used) ← MISMATCH!

❌ http://localhost:7001/signin-churchsuite (HTTP, not HTTPS)
✅ https://localhost:7001/signin-churchsuite (HTTPS)
```

### OAuth Redirect Not Happening

**Cause:** Configuration not loaded or OAuth provider not registered

**Fix:**
1. Check application logs (look for "ChurchSuite OAuth" messages)
2. Verify all three secrets are in Key Vault
3. Check that `Program.cs` has OAuth configured
4. Restart application after adding secrets

### Profile Not Syncing

**Cause:** OAuth callback succeeded but profile fetch failed

**Fix:**
1. Check logs for profile sync errors
2. Verify ChurchSuite API endpoints are correct
3. Check that OAuth token is being saved (verify on debug page)
4. Ensure ChurchSuite account has permission to read profile

## Configuration Priority

The system tries to load configuration in this order (first match wins):

1. **Environment Variables** (highest priority)
   - `CHURCHSUITE_OAUTH_CLIENT_ID`
   - `CHURCHSUITE_OAUTH_CLIENT_SECRET`
   - `CHURCHSUITE_OAUTH_CALLBACK_URL`

2. **Azure Key Vault**
   - `churchsuite-oauth-client-id`
   - `churchsuite-oauth-client-secret`
   - `churchsuite-oauth-callback-url`

3. **appsettings.json** (lowest priority)
   - `ChurchSuite:OAuth:ClientId`
   - `ChurchSuite:OAuth:ClientSecret`
   - `ChurchSuite:OAuth:CallbackUrl`

This means:
- Production should use Key Vault
- Development can use Environment Variables or appsettings
- Environment Variables override everything

## Security Notes

✅ **Do's:**
- Store secrets in Key Vault (production)
- Use HTTPS for callback URLs
- Don't commit secrets to Git
- Regenerate secrets if compromised
- Monitor OAuth logs

❌ **Don'ts:**
- Don't hardcode secrets in code
- Don't use HTTP in production
- Don't share Client Secret
- Don't commit appsettings with secrets
- Don't leave debug endpoint accessible in production

## API Reference

### OAuthCallbackController Endpoints

#### POST /signin-churchsuite
Receives OAuth callback from ChurchSuite.

**Automatic:** Called by ChurchSuite after authentication.

**Response:** Redirects to home page or return URL.

#### GET /oauth-debug
Returns current OAuth configuration and user state.

**Response:**
```json
{
  "status": "debug_info",
  "callbackUrl": "https://localhost:7001/signin-churchsuite",
  "currentUser": "user@example.com",
  "isAuthenticated": true,
  "claims": [...],
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## Next Steps

1. ✅ Register OAuth app in ChurchSuite
2. ✅ Determine your callback URL format
3. ✅ Store credentials (Key Vault or env vars)
4. ✅ Start application
5. ✅ Test OAuth login flow
6. ✅ Verify profile syncing
7. ✅ Monitor logs for errors
8. ✅ Deploy to production

## Files Modified/Created

- ✅ `Controllers/OAuthCallbackController.cs` - NEW
- ✅ `Components/Configuration/ChurchSuiteOAuthConfiguration.cs` - NEW
- ✅ `Program.cs` - MODIFIED (OAuth setup and events)
- ✅ `OAUTH_CALLBACK_CONFIGURATION.md` - NEW (technical reference)
- ✅ `appsettings.ChurchSuiteOAuth.example.json` - NEW (example config)

## Support & Resources

- OAuth Debug Endpoint: `/oauth-debug`
- Application Logs: Check Serilog output for "ChurchSuite OAuth" messages
- Error Messages: Check browser console and network tab
- Documentation: `OAUTH_CALLBACK_CONFIGURATION.md`

## Quick Checklist for Production Deployment

- [ ] ChurchSuite OAuth app created
- [ ] Client ID stored in Key Vault as `churchsuite-oauth-client-id`
- [ ] Client Secret stored in Key Vault as `churchsuite-oauth-client-secret`
- [ ] Callback URL stored in Key Vault as `churchsuite-oauth-callback-url`
- [ ] Callback URL matches ChurchSuite OAuth app settings
- [ ] Application successfully builds
- [ ] OAuth login works end-to-end
- [ ] Profile syncing works
- [ ] Debug endpoint removed or protected
- [ ] Logs monitored for errors
- [ ] Certificate is valid (HTTPS)

---

**Last Updated:** 2024
**Version:** 1.0
**Author:** ForestChurches Development Team
