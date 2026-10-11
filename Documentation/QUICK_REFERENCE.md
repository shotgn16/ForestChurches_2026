# 🎯 Quick Reference Card - ChurchSuite PKCE Testing

## Five Seconds? Read This:

**To test:** `dotnet run` → Click "Sign In with ChurchSuite" → Complete auth → Check database

---

## One Minute? Do This:

```bash
# 1. Start the app
dotnet run

# 2. Open in browser
https://localhost:5001

# 3. Navigate to registration
/Identity/Account/Register

# 4. Click "Sign In with ChurchSuite"

# 5. Complete authentication

# 6. Check database
# Open SQL Server Management Studio or Azure Data Studio
# Run: SELECT * FROM ChurchSuiteCredentials WHERE UserId = '<your-id>'

# 7. Verify:
#    - AccessToken: not null ✅
#    - RefreshToken: not null ✅
#    - ExpiresAt: future date ✅
#    - IsActive: 1 (true) ✅
```

---

## Five Minutes? Execute This:

See: `QUICK_TEST_STEPS.md`

---

## Full Testing? Do This:

See: `TESTING_GUIDE.md`

---

## Automated Tests? Do This:

See: `AUTOMATED_TESTS.md`

---

## Architecture? See This:

See: `CHURCHSUITE_PKCE_IMPLEMENTATION.md`

---

## Visual Guide? See This:

See: `TESTING_FLOWCHART.md`

---

## Navigation Help? See This:

See: `START_HERE_TESTING.md`

---

## Test Flow Summary

```
1. User clicks "Sign In with ChurchSuite"
   ↓
2. Redirected to ChurchSuite login page
   ↓
3. User enters ChurchSuite credentials
   ↓
4. Redirected back to /signin-churchsuite?code=...
   ↓
5. Token is exchanged for access/refresh tokens
   ↓
6. Tokens stored in ChurchSuiteCredentials table
   ↓
7. User is authenticated
   ✅ Success!
```

---

## Critical Database Columns

```sql
ChurchSuiteCredentials
├─ AccessToken ................ Current API token
├─ RefreshToken ............... Token to get new access token
├─ ExpiresAt .................. When token expires (should be future)
├─ IsActive ................... Should be 1/true
├─ UserId ..................... Matches logged-in user
└─ UpdatedAt .................. Should be recent timestamp
```

---

## Expected After Auth

✅ **In Browser:** Signed in and on home page  
✅ **In Database:** New ChurchSuiteCredentials record  
✅ **In Logs:** No errors or warnings  
✅ **Token Valid:** AccessToken can call ChurchSuite API  
✅ **Token Expires:** ExpiresAt is future date  

---

## Troubleshooting Quick Answers

| What happened | Check |
|---------------|-------|
| Button missing | ExternalLogin.cshtml updated? |
| Redirect fails | ClientId configured? |
| No database record | Token exchange completed? |
| Token is null | RefreshToken exists? |
| API call fails | Token not expired? |
| User not logged in | User record created? |

---

## SQL Queries You'll Need

```sql
-- See all credentials
SELECT * FROM ChurchSuiteCredentials;

-- See credentials for specific user
SELECT * FROM ChurchSuiteCredentials 
WHERE UserId = 'your-user-id';

-- Check if token is expired
SELECT UserId, AccessToken, ExpiresAt, 
	   CASE WHEN ExpiresAt < GETUTCDATE() THEN 'EXPIRED' 
			ELSE 'VALID' END as TokenStatus
FROM ChurchSuiteCredentials;

-- Delete test credentials
DELETE FROM ChurchSuiteCredentials 
WHERE UserId = 'test-user-id';

-- See user's ClientId
SELECT Id, ClientId FROM AspNetUsers 
WHERE Id = 'your-user-id';
```

---

## Key Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/churchsuite/login-pkce` | Start PKCE flow |
| `/signin-churchsuite` | Callback from ChurchSuite |
| `/churchsuite/check-auth` | Check if user authorized |

---

## Most Common Issues

### ❌ "No such table: ChurchSuiteCredentials"
**Fix:** Run migrations  
```bash
dotnet ef database update
```

### ❌ "ClientId not configured"
**Fix:** Add to appsettings.json  
```json
"ChurchSuiteOAuth": {
  "ClientId": "your-client-id"
}
```

### ❌ "PKCE verifier not found in session"
**Fix:** Session might have expired - retry auth flow

### ❌ "Token exchange returned 400"
**Fix:** Check that code_verifier matches code_challenge

### ❌ "User not found after callback"
**Fix:** User might not be registered - registration page shows after auth

---

## Success Checklist

- [ ] Application builds
- [ ] Can navigate to registration
- [ ] "Sign In with ChurchSuite" button visible
- [ ] Can click button without error
- [ ] Redirected to ChurchSuite login
- [ ] Can log in to ChurchSuite
- [ ] Redirected back to your app
- [ ] Database has new credential record
- [ ] AccessToken is not null
- [ ] RefreshToken is not null
- [ ] ExpiresAt is a future date
- [ ] User is logged in

---

## Files Modified

```
✅ Components/Http/ChurchSuiteTokenService.cs (CREATED)
✅ Components/Http/ChurchSuiteAuthenticationService.cs (CREATED)
✅ Controllers/ChurchsuiteController.cs (MODIFIED)
✅ Controllers/OAuthCallbackController.cs (MODIFIED)
✅ Components/Http/ChurchSuiteService.cs (MODIFIED)
✅ Components/Http/ChurchSuiteEventSyncService.cs (MODIFIED)
✅ Areas/Identity/Pages/Account/ExternalLogin.cshtml (MODIFIED)
✅ Pages/Shared/ChurchSuiteAuthStatus.cshtml (CREATED)
✅ Areas/Identity/Data/ForestChurchesContext.cs (MODIFIED)
✅ Components/Configuration/ServiceRegistration.cs (MODIFIED)
```

---

## Testing Documents Available

- `START_HERE_TESTING.md` ........... Navigation guide
- `CHURCHSUITE_PKCE_IMPLEMENTATION.md` ... Architecture
- `TESTING_SUMMARY.md` ............ Overview & checklist
- `QUICK_TEST_STEPS.md` ........... 5-minute test
- `TESTING_GUIDE.md` ............. Comprehensive test
- `AUTOMATED_TESTS.md` ........... Unit & integration tests
- `TESTING_FLOWCHART.md` ......... Visual diagrams
- `README_TESTING.md` ............ This index

---

## Next Action

Pick **ONE**:

| Time | Action |
|------|--------|
| 5 min | `dotnet run` then click button |
| 10 min | Follow QUICK_TEST_STEPS.md |
| 30 min | Follow TESTING_GUIDE.md |
| 2 hours | Follow AUTOMATED_TESTS.md |

---

## Environment Check

Before testing, verify:

```bash
# 1. Build succeeds
dotnet build

# 2. Database has ChurchSuiteCredentials table
# Run SQL: SELECT * FROM ChurchSuiteCredentials

# 3. ChurchSuite account ready
# Have username/password available

# 4. ClientId configured
# In appsettings.json or environment variable
```

---

## Common Commands

```bash
# Start app
dotnet run

# Clean build
dotnet clean
dotnet build

# Run tests (when created)
dotnet test

# Update database
dotnet ef database update

# View logs
tail -f logs.txt

# Clear database (dev only!)
dotnet ef database drop --force
dotnet ef database update
```

---

## Success = 

✅ Build passes  
✅ PKCE flow works  
✅ Credentials stored  
✅ Token refresh works  
✅ No hardcoded secrets  
✅ Error handling works  
✅ Users can sign in  
✅ All tests pass  

---

**Now go test it! 🚀**

Start with: `dotnet run`
