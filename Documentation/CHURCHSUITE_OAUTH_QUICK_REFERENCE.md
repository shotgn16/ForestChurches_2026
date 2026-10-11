# ChurchSuite OAuth Callback - Quick Reference

## Callback URL Format

```
https://your-domain.com/signin-churchsuite
```

## Key Vault Secrets Required

```
churchsuite-oauth-client-id
churchsuite-oauth-client-secret
churchsuite-oauth-callback-url
```

## Environment Variables (Alternative)

```bash
CHURCHSUITE_OAUTH_CLIENT_ID
CHURCHSUITE_OAUTH_CLIENT_SECRET
CHURCHSUITE_OAUTH_CALLBACK_URL
```

## Endpoints

| Endpoint | Purpose | Example |
|----------|---------|---------|
| `/signin-churchsuite` | OAuth callback handler | https://localhost:7001/signin-churchsuite |
| `/oauth-debug` | Debug OAuth config | https://localhost:7001/oauth-debug |
| `/Identity/Account/Login` | Login page with button | https://localhost:7001/Identity/Account/Login |

## Configuration Files

- `Program.cs` (lines 264-315) - OAuth provider setup
- `Controllers/OAuthCallbackController.cs` - Callback handler
- `Components/Configuration/ChurchSuiteOAuthConfiguration.cs` - Config helper

## Testing Checklist

- [ ] ChurchSuite OAuth app registered
- [ ] Credentials stored in Key Vault
- [ ] Callback URL is valid HTTPS URL
- [ ] Callback URL matches ChurchSuite settings
- [ ] Application builds successfully
- [ ] Can access debug endpoint: `/oauth-debug`
- [ ] Login page shows "Login with ChurchSuite" button
- [ ] Clicking button redirects to ChurchSuite
- [ ] After auth, redirected back to `/signin-churchsuite`
- [ ] User is signed in
- [ ] Profile is synced from ChurchSuite
- [ ] No errors in logs

## Common Issues & Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| "Invalid Client" | Wrong credentials | Regenerate in ChurchSuite |
| "Redirect URI Mismatch" | URL doesn't match | Verify in ChurchSuite AND Key Vault |
| Redirect not happening | Config not loaded | Check Key Vault/env vars |
| Profile not syncing | OAuth token issue | Check logs, verify token saved |

## Local Development Setup

```bash
# Option 1: Environment Variables (Windows PowerShell)
$env:CHURCHSUITE_OAUTH_CLIENT_ID = "your-id"
$env:CHURCHSUITE_OAUTH_CLIENT_SECRET = "your-secret"
$env:CHURCHSUITE_OAUTH_CALLBACK_URL = "https://localhost:7001/signin-churchsuite"

# Option 2: appsettings.Development.json
Copy appsettings.ChurchSuiteOAuth.example.json to appsettings.Development.json
Edit with your credentials

# Start app
dotnet run
```

## Debugging Steps

1. Check debug endpoint: `https://localhost:7001/oauth-debug`
2. Look for "ChurchSuite OAuth" in application logs
3. Verify secrets in Key Vault/env vars
4. Test OAuth button on login page
5. Check browser network tab for redirect
6. Review error messages in logs

## Key Files

```
Controllers/
  └─ OAuthCallbackController.cs          ← Handles /signin-churchsuite

Components/Configuration/
  └─ ChurchSuiteOAuthConfiguration.cs    ← Loads & validates config

Program.cs                                ← OAuth provider setup (lines 264-315)

Documentation/
  ├─ OAUTH_CALLBACK_CONFIGURATION.md     ← Technical reference
  ├─ CHURCHSUITE_OAUTH_CALLBACK_SETUP.md ← Complete setup guide
  └─ CHURCHSUITE_OAUTH_QUICK_REFERENCE.md ← This file
```

## OAuth Flow Diagram

```
User clicks "Login with ChurchSuite"
		↓
Redirected to ChurchSuite OAuth
		↓
User enters credentials
		↓
ChurchSuite redirects to /signin-churchsuite
		↓
OAuthCallbackController.SignInChurchSuite()
		↓
User is authenticated
		↓
Profile synced from ChurchSuite
		↓
Redirect to home or return URL
```

## Production Checklist

Before deploying to production:

```
Security:
  [ ] All secrets in Key Vault
  [ ] HTTPS enabled on callback URL
  [ ] Debug endpoint removed or protected
  [ ] Certificates valid

Configuration:
  [ ] Callback URL set to production domain
  [ ] OAuth app registered in ChurchSuite
  [ ] Client ID and Secret configured
  [ ] Key Vault secrets populated

Testing:
  [ ] OAuth login works end-to-end
  [ ] Profile syncing verified
  [ ] No errors in production logs
  [ ] Tested with real ChurchSuite account

Monitoring:
  [ ] Logs monitored for OAuth errors
  [ ] Alert configured for failures
  [ ] Rate limiting configured (if needed)
```

## Support Resources

- **Technical Details:** See `OAUTH_CALLBACK_CONFIGURATION.md`
- **Complete Setup:** See `CHURCHSUITE_OAUTH_CALLBACK_SETUP.md`
- **Logs:** Search for "ChurchSuite OAuth" in application logs
- **Debug:** Visit `/oauth-debug` endpoint
- **Code:** `Controllers/OAuthCallbackController.cs`

---

**Last Updated:** 2024
**Version:** 1.0
