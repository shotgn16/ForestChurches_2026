# ChurchSuite PKCE Implementation - Testing Guide

## Quick Start Testing

### 1. **Basic PKCE Flow Test** (Manual - Recommended First)

#### Prerequisites
- Application running locally or in dev environment
- ChurchSuite account with OAuth app configured
- Global ClientId in appsettings or Key Vault
- Session storage configured

#### Steps
1. **Start the application**
   ```powershell
   dotnet run
   ```
   Navigate to: `https://localhost:5001`

2. **Test the PKCE Login Flow**
   - Go to: `/Identity/Account/Register` or `/Identity/Account/Login`
   - Look for "Sign In with ChurchSuite" button
   - Click the button
   - **Expected Result**: Redirected to ChurchSuite login page (login.churchsuite.com)

3. **Complete ChurchSuite Authentication**
   - Enter your ChurchSuite credentials
   - Click "Authorize" when prompted
   - **Expected Result**: Redirected back to your app at `/signin-churchsuite?code=...&state=...`

4. **Verify Credential Storage**
   - If creating new account: You'll be on the registration page
   - If existing user: You'll be signed in and redirected to home page
   - Check database:
	 ```sql
	 SELECT * FROM ChurchSuiteCredentials WHERE UserId = '<your-user-id>';
	 ```
   - **Expected Result**: Record exists with `AccessToken`, `RefreshToken`, `ExpiresAt` populated

---

### 2. **Token Refresh Test**

#### Test Automatic Refresh on Expired Token

1. **Manually expire a token in database**
   ```sql
   UPDATE ChurchSuiteCredentials 
   SET ExpiresAt = NOW() - INTERVAL 1 HOUR
   WHERE UserId = '<your-user-id>';
   ```

2. **Trigger an action that requires ChurchSuite API**
   - Click "Sync Events" or similar feature that calls `ChurchSuiteService`
   - **Expected Result**: 
	 - Token is automatically refreshed
	 - `LastRefreshedAt` is updated
	 - API call succeeds with new token

3. **Verify in logs**
   - Check application logs for: `"Token expired, attempting refresh"`
   - Should see: `"Token refreshed successfully"`

---

### 3. **Per-User ClientId Fallback Test**

#### Test using user-provided ClientId (fallback flow)

1. **Disable global ClientId**
   - Comment out or remove `ChurchSuiteOAuth:ClientId` from appsettings.json

2. **Set user's ClientId**
   ```sql
   UPDATE AspNetUsers 
   SET ClientId = 'your-churchsuite-client-id'
   WHERE Id = '<user-id>';
   ```

3. **Initiate PKCE flow**
   - Click "Sign In with ChurchSuite"
   - **Expected Result**: 
	 - Still redirected to ChurchSuite (uses user's ClientId)
	 - Authorization succeeds
	 - Tokens stored correctly

4. **Verify in logs**
   - Should see: `"Using per-user ClientId for token exchange"`

---

### 4. **Authorization Status Check Test**

#### Test the `/churchsuite/check-auth` endpoint

1. **Open browser developer tools** (F12)

2. **Make API request**
   ```javascript
   // In browser console:
   fetch('/churchsuite/check-auth?user_id=your-user-id')
	 .then(r => r.json())
	 .then(d => console.log(d))
   ```

3. **Expected Response**
   ```json
   {
	 "hasAuthorization": true,
	 "hasValidToken": true
   }
   ```

4. **Test with expired token**
   - Expire token in database (see step 2.1)
   - Make same API request
   - **Expected Response**
   ```json
   {
	 "hasAuthorization": true,
	 "hasValidToken": false
   }
   ```

---

### 5. **Session Management Test**

#### Verify PKCE session data is stored and cleared properly

1. **Enable session debugging**
   - Add logging in `ChurchSuiteAuthenticationService.cs`:
	 ```csharp
	 _logger.LogInformation("Storing PKCE verifier in session: {SessionKey}", verifierKey);
	 ```

2. **Initiate PKCE flow**
   - Click "Sign In with ChurchSuite"
   - Check logs for session storage confirmation

3. **Complete OAuth callback**
   - Return from ChurchSuite
   - Check logs for: `"PKCE session data cleared"`
   - **Expected Result**: Session keys are removed after use

---

## Automated Testing

### Unit Tests for ChurchSuiteTokenService

Create file: `Tests/Components/Http/ChurchSuiteTokenServiceTests.cs`

```csharp
using Xunit;
using Moq;
using ForestChurches.Components.Http;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.EntityFrameworkCore;

public class ChurchSuiteTokenServiceTests
{
	[Fact]
	public async Task GetValidAccessTokenAsync_WithValidToken_ReturnsToken()
	{
		// Arrange
		var userId = "test-user-123";
		var validToken = "valid-access-token";
		var mockContext = new Mock<ForestChurchesContext>();

		var credential = new ChurchSuiteCredential
		{
			UserId = userId,
			AccessToken = validToken,
			ExpiresAt = DateTime.UtcNow.AddHours(1),
			IsActive = true
		};

		// Act
		// Assert
		// TODO: Implement actual test
	}

	[Fact]
	public async Task GetValidAccessTokenAsync_WithExpiredToken_RefreshesToken()
	{
		// Arrange - token is expired
		var userId = "test-user-123";
		var expiredToken = "expired-token";
		var refreshToken = "refresh-token-123";

		// Act
		// Assert
		// TODO: Implement actual test
	}

	[Fact]
	public async Task RefreshTokenAsync_WithValidRefreshToken_UpdatesToken()
	{
		// Arrange
		var credential = new ChurchSuiteCredential
		{
			UserId = "test-user-123",
			RefreshToken = "valid-refresh-token",
			ClientId = "test-client-id"
		};

		// Act
		// Assert
		// TODO: Implement actual test
	}
}
```

### Integration Tests for OAuth Flow

Create file: `Tests/Controllers/OAuthCallbackControllerTests.cs`

```csharp
using Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

public class OAuthCallbackControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
	private readonly WebApplicationFactory<Program> _factory;

	public OAuthCallbackControllerTests(WebApplicationFactory<Program> factory)
	{
		_factory = factory;
	}

	[Fact]
	public async Task SignInChurchSuite_WithValidCode_ExchangesForToken()
	{
		// Arrange
		var client = _factory.CreateClient();
		var state = "test-state-123";
		var code = "test-auth-code-456";

		// Act
		var response = await client.GetAsync($"/signin-churchsuite?code={code}&state={state}");

		// Assert
		Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
	}

	[Fact]
	public async Task SignInChurchSuite_WithoutCode_RedirectsToLogin()
	{
		// Arrange
		var client = _factory.CreateClient();

		// Act
		var response = await client.GetAsync("/signin-churchsuite");

		// Assert
		Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		Assert.Contains("Login", response.Headers.Location?.OriginalString ?? "");
	}
}
```

---

## Manual Testing Checklist

### User Registration Flow
- [ ] Navigate to registration page
- [ ] Click "Sign In with ChurchSuite"
- [ ] Complete ChurchSuite authentication
- [ ] Register new account
- [ ] Verify credentials stored in database
- [ ] Verify user is signed in after registration

### Existing User Flow
- [ ] Sign in to existing account manually
- [ ] Click "Sign In with ChurchSuite" again
- [ ] Complete ChurchSuite authentication
- [ ] Verify redirected to home page (not registration)
- [ ] Verify credentials updated in database

### Token Expiration Flow
- [ ] Manually set token expiry to past date
- [ ] Click feature that requires ChurchSuite API (event sync)
- [ ] Verify token is refreshed automatically
- [ ] Verify feature works without user interaction

### Error Handling
- [ ] No ClientId configured: Should show error
- [ ] Invalid authorization code: Should redirect to login
- [ ] Token refresh fails: Should redirect to authorize again
- [ ] Network timeout: Should log error and handle gracefully

### Session Handling
- [ ] Close browser completely
- [ ] Start new session
- [ ] PKCE data should not be available (old session expired)
- [ ] User must re-authorize

---

## Debugging Tips

### Enable Verbose Logging

In `Program.cs`, configure logging:

```csharp
builder.Services.AddLogging(config =>
{
	config.AddConsole();
	config.AddDebug();
	config.SetMinimumLevel(LogLevel.Debug);
});
```

### Monitor Database Changes

Watch `ChurchSuiteCredentials` table:

```sql
-- Terminal/PowerShell window
WHILE($true) {
	Clear-Host
	# Execute query to show latest credentials
	sqlcmd -S localhost -d ForestChurches -Q "SELECT * FROM ChurchSuiteCredentials ORDER BY UpdatedAt DESC"
	Start-Sleep -Seconds 5
}
```

### Check Session Storage

In development, session is stored in memory. To inspect:

```csharp
// In a controller action
var sessionData = HttpContext.Session.GetString($"pkce:test-state:verifier");
_logger.LogInformation("Session PKCE verifier: {Verifier}", sessionData);
```

### Trace PKCE Code Challenge

Add logging to `ChurchSuiteAuthenticationService.GeneratePKCEAuthorizationUrlAsync`:

```csharp
_logger.LogInformation("Generated code_challenge: {Challenge}", codeChallenge);
_logger.LogInformation("Code verifier: {Verifier}", codeVerifier);
_logger.LogInformation("Authorization URL: {Url}", authUrl);
```

---

## Testing Against Live ChurchSuite (Optional)

### Prerequisites
1. Real ChurchSuite account with OAuth app registered
2. OAuth app configured with your app's callback URL
3. Real ClientId from ChurchSuite dashboard

### Steps
1. Update appsettings with real ClientId
2. Ensure callback URL matches ChurchSuite OAuth app settings
3. Run the manual tests above with real ChurchSuite credentials
4. Verify events sync correctly from your ChurchSuite account

---

## Common Issues & Troubleshooting

### Issue: "PKCE code_verifier not found in session"
**Cause**: Session expired between authorization and callback
**Solution**: 
- Check session timeout is reasonable (>5 minutes)
- Verify cookie SameSite is set to Lax/None
- Check browser allows third-party cookies

### Issue: "No ClientId configured"
**Cause**: Missing global ClientId and user doesn't have per-user ClientId
**Solution**:
- Add ClientId to appsettings.json
- Or set ClientId on user record in database
- Or pass ClientId as query parameter in PKCE flow

### Issue: Token refresh fails with 400 error
**Cause**: Invalid refresh_token or ClientId mismatch
**Solution**:
- Verify refresh_token matches original authorization
- Verify ClientId is consistent (same one used for initial auth)
- Check ChurchSuite API status

### Issue: "User not found after callback"
**Cause**: User ID in session doesn't match database user
**Solution**:
- Check user exists in database
- Verify user ID format matches (should be GUID string)
- Check for case sensitivity issues

---

## Success Criteria

You'll know the implementation is working correctly when:

✅ Users can sign in/register with ChurchSuite without providing secrets  
✅ Tokens are automatically refreshed when expired  
✅ Per-user ClientId fallback works if global ClientId unavailable  
✅ All ChurchSuite API calls work consistently  
✅ Session data is properly managed (stored and cleared)  
✅ Errors are handled gracefully with appropriate logging  
✅ No hardcoded secrets in code or configuration  
✅ Build completes successfully  

---

## Next Steps After Testing

1. **Deploy to staging environment**
   - Test with actual ChurchSuite prod account
   - Load test with multiple concurrent users

2. **Monitor logs in production**
   - Watch for authorization failures
   - Monitor token refresh patterns
   - Alert on any errors

3. **Document for end users**
   - Create user guide for authorizing with ChurchSuite
   - Document what happens if authorization is revoked
   - Explain re-authorization process

4. **Plan rollout**
   - Migrate existing users' credentials
   - Communicate changes to users
   - Have rollback plan ready
