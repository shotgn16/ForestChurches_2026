# ChurchSuite OAuth Callback Configuration Guide

## Overview

This guide explains how to configure the ChurchSuite OAuth callback URL for ForestChurches. The callback URL is critical for the OAuth flow to work properly.

## What is an OAuth Callback URL?

After a user authenticates with ChurchSuite, they are redirected back to your application with an authorization code. The callback URL is where ChurchSuite sends this redirect.

**Flow:**
```
User clicks "Login with ChurchSuite"
	↓
Redirected to ChurchSuite OAuth (https://oauth.churchsuite.com/oauth/authorize)
	↓
User enters ChurchSuite credentials
	↓
ChurchSuite redirects back to YOUR callback URL with authorization code
	↓
ForestChurches exchanges code for access token
	↓
User is signed in and profile is synced
```

## Implementation Details

### Callback Endpoint

**Location:** `Controllers/OAuthCallbackController.cs`

**Route:** `/signin-churchsuite`

The controller handles:
- Receiving the OAuth callback from ChurchSuite
- Verifying authentication succeeded
- Redirecting user appropriately
- Logging OAuth events for debugging

### Configuration

#### 1. **Key Vault Secrets Required**

Add these three secrets to Azure Key Vault:

```
Secret Name: churchsuite-oauth-client-id
Value: [Your ChurchSuite OAuth App Client ID]

Secret Name: churchsuite-oauth-client-secret
Value: [Your ChurchSuite OAuth App Client Secret]

Secret Name: churchsuite-oauth-callback-url
Value: [Your callback URL - see below]
```

#### 2. **Callback URL Format**

The callback URL must be:
- **Absolute URL** (include scheme and domain)
- **HTTPS in production** (HTTP allowed only in development)
- **Match exactly** what's registered in ChurchSuite

**Examples:**

Development:
```
https://localhost:7001/signin-churchsuite
```

Production:
```
https://forestchurches.co.uk/signin-churchsuite
https://www.forestchurches.co.uk/signin-churchsuite
```

#### 3. **Program.cs Configuration**

The OAuth provider is configured in `Program.cs` (lines 264-315):

```csharp
.AddOAuth("ChurchSuite", options =>
{
	options.ClientId = churchSuiteClientId;
	options.ClientSecret = churchSuiteClientSecret;
	options.CallbackPath = "/signin-churchsuite";  // This is the path part

	// Endpoints
	options.AuthorizationEndpoint = "https://oauth.churchsuite.com/oauth/authorize";
	options.TokenEndpoint = "https://oauth.churchsuite.com/oauth/token";
	options.UserInformationEndpoint = "https://oauth.churchsuite.com/oauth/me";

	options.Scope.Add("profile:read");
	options.Scope.Add("account:read");
	options.SaveTokens = true;

	// OAuth Events for debugging and error handling
	options.Events = new OAuthEvents { ... };
})
```

## Setup Steps

### Step 1: Determine Your Callback URL

Decide what your callback URL will be:

**For Development:**
- If running locally on `localhost:7001`: Use `https://localhost:7001/signin-churchsuite`
- If using ngrok: Use `https://your-ngrok-url.ngrok.io/signin-churchsuite`

**For Production:**
- Use your domain: `https://forestchurches.co.uk/signin-churchsuite`
- Or with www: `https://www.forestchurches.co.uk/signin-churchsuite`

### Step 2: Register Callback URL in ChurchSuite

1. Log in to ChurchSuite admin panel
2. Navigate to OAuth/App Registration settings
3. Create or edit your OAuth application
4. Add your callback URL to the "Redirect URIs" field
5. Save the OAuth app configuration

### Step 3: Store Credentials in Key Vault

Using Azure CLI or Azure Portal:

```bash
# Using Azure CLI
az keyvault secret set --vault-name "your-key-vault-name" \
	--name "churchsuite-oauth-client-id" \
	--value "your-client-id"

az keyvault secret set --vault-name "your-key-vault-name" \
	--name "churchsuite-oauth-client-secret" \
	--value "your-client-secret"

az keyvault secret set --vault-name "your-key-vault-name" \
	--name "churchsuite-oauth-callback-url" \
	--value "https://your-domain.com/signin-churchsuite"
```

### Step 4: Test the Callback URL

1. Start the application
2. Navigate to the login page
3. Click "Login with ChurchSuite"
4. You should be redirected to ChurchSuite OAuth login
5. After entering credentials, you should be redirected back to `/signin-churchsuite`
6. The callback controller will process the request and sign you in

## Troubleshooting

### "Redirect URI mismatch" Error

**Cause:** The callback URL in ChurchSuite doesn't match what your application is using.

**Solution:**
1. Verify the callback URL in ChurchSuite settings
2. Verify the `churchsuite-oauth-callback-url` in Key Vault
3. They must match exactly (including http/https, www, trailing slashes, etc.)

### Callback Not Being Called

**Cause:** OAuth provider configuration issue or ChurchSuite settings incorrect.

**Solution:**
1. Check browser network tab to see if redirect happens
2. Verify client ID and secret are correct
3. Check logs at `/oauth-debug` endpoint for debugging info
4. Ensure HTTPS is used (not HTTP)

### "Invalid Client" Error

**Cause:** Client ID or Client Secret is incorrect.

**Solution:**
1. Verify the values in Key Vault match ChurchSuite exactly
2. Check for extra spaces or special characters
3. Regenerate credentials in ChurchSuite if needed

## OAuth Debug Endpoint

For development/troubleshooting, access the debug endpoint:

```
https://your-domain.com/oauth-debug
```

This returns:
- Callback URL being used
- Current user information
- Current claims
- Authentication status
- Timestamp

**Note:** Remove or protect this endpoint in production.

## Security Considerations

1. **HTTPS Required:** Always use HTTPS in production
2. **Secret Management:** Never hardcode secrets; always use Key Vault
3. **Callback URL Must Be HTTPS:** ChurchSuite will reject HTTP callbacks in production
4. **Validate State:** ASP.NET Core handles CSRF token validation automatically
5. **Token Storage:** Access tokens are stored securely in user claims

## Monitoring and Logging

The OAuth flow logs events to Serilog:

- Successfully created authentication ticket
- Remote authentication failures
- Access denied events
- Errors during OAuth callback

Check application logs for troubleshooting.

## Related Files

- `Controllers/OAuthCallbackController.cs` - OAuth callback handler
- `Program.cs` - OAuth configuration (lines 264-315)
- `Areas/Identity/Pages/Account/ExternalLogin.cshtml.cs` - Profile syncing
- `Areas/Identity/Pages/Account/Login.cshtml` - Login button

## Next Steps

1. ✅ Register OAuth app in ChurchSuite
2. ✅ Get Client ID and Client Secret
3. ✅ Store credentials in Key Vault with correct secret names
4. ✅ Verify callback URL matches between ChurchSuite and Key Vault
5. ✅ Test OAuth login flow
6. ✅ Monitor logs for any issues

## Support

If you encounter issues:
1. Check the debug endpoint: `/oauth-debug`
2. Review application logs in Azure
3. Verify all three Key Vault secrets are present
4. Confirm callback URL format is absolute URL with scheme
5. Test with a simple ChurchSuite account first
