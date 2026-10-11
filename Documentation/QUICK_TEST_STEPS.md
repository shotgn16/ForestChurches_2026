# Quick Testing Steps - Visual Guide

## 🚀 Start Here: 5-Minute Test

### Step 1: Run the Application
```powershell
cd "C:\Users\JackBarnard\OneDrive - OptimiDoc s.r.o\Desktop\Other\ForestChurches (19.10.2025)"
dotnet run
```

Output should show:
```
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
	  Request starting HTTP/1.1 GET https://localhost:5001/
...
Now listening on: https://localhost:5001
```

### Step 2: Navigate to Registration Page
```
https://localhost:5001/Identity/Account/Register
```

You should see a page with:
- Email input field
- Password input field
- ✅ **"Sign In with ChurchSuite" button** (look for this!)

### Step 3: Click "Sign In with ChurchSuite" Button
When you click it, you should be redirected to:
```
https://login.churchsuite.com/oauth2/authorize?
  client_id=YOUR_CLIENT_ID
  &response_type=code
  &scope=account
  &redirect_uri=https%3A%2F%2Flocalhost%3A5001%2Fsignin-churchsuite
  &code_challenge=...
  &code_challenge_method=S256
  &state=...
```

### Step 4: Log In to ChurchSuite
- Enter your ChurchSuite credentials
- Click "Sign In"
- You'll see an authorization prompt
- Click "Authorize" to allow the app access

### Step 5: Return to Your Application
You'll be redirected back to:
```
https://localhost:5001/signin-churchsuite?code=AUTH_CODE&state=STATE_VALUE
```

Then automatically processed to:
- **New user**: Registration completion page
- **Existing user**: Home page (signed in)

### Step 6: Verify Success
Open your database and run:
```sql
SELECT Id, UserId, AccessToken, RefreshToken, ExpiresAt, IsActive 
FROM ChurchSuiteCredentials 
ORDER BY UpdatedAt DESC 
LIMIT 1;
```

You should see a new record with all tokens populated ✅

---

## 🔄 Token Refresh Test (10 minutes)

### Step 1: Get User ID
```sql
SELECT Id, Email FROM AspNetUsers WHERE Email = 'your-email@example.com';
```
Copy the `Id` value.

### Step 2: Expire the Token
```sql
UPDATE ChurchSuiteCredentials 
SET ExpiresAt = NOW() - INTERVAL 1 HOUR
WHERE UserId = 'PASTE_USER_ID_HERE';
```

### Step 3: Trigger API Call
In the application, click a button that syncs ChurchSuite data (like "Sync Events").

### Step 4: Check Application Logs
Look for:
```
Token expired, attempting refresh
Successfully refreshed token
```

### Step 5: Verify Token Updated
```sql
SELECT LastRefreshedAt, ExpiresAt FROM ChurchSuiteCredentials 
WHERE UserId = 'PASTE_USER_ID_HERE';
```

`LastRefreshedAt` should be recent (within last minute) ✅

---

## 🧪 Test Endpoints

### Check Authorization Status
```powershell
# PowerShell
$userId = "YOUR_USER_ID"
$response = Invoke-WebRequest -Uri "https://localhost:5001/churchsuite/check-auth?user_id=$userId" -SkipCertificateCheck
$response.Content | ConvertFrom-Json | Format-List
```

Expected output:
```
hasAuthorization : True
hasValidToken    : True
```

### Manual PKCE Initiation
```
https://localhost:5001/churchsuite/login-pkce?redirect_uri=https://localhost:5001/home
```

Should redirect to ChurchSuite login ✅

---

## 📊 Database Inspection Commands

### View All ChurchSuite Credentials
```sql
SELECT 
  Id,
  UserId,
  Subdomain,
  IsActive,
  CreatedAt,
  UpdatedAt,
  LastRefreshedAt,
  ExpiresAt
FROM ChurchSuiteCredentials
ORDER BY UpdatedAt DESC;
```

### View Specific User's Credentials
```sql
SELECT 
  c.*,
  u.Email
FROM ChurchSuiteCredentials c
JOIN AspNetUsers u ON c.UserId = u.Id
WHERE u.Email = 'user@example.com';
```

### Check Token Expiration
```sql
SELECT 
  UserId,
  CASE 
	WHEN ExpiresAt IS NULL THEN 'NULL'
	WHEN ExpiresAt < NOW() THEN 'EXPIRED'
	WHEN ExpiresAt < DATE_ADD(NOW(), INTERVAL 1 HOUR) THEN 'EXPIRING SOON'
	ELSE 'VALID'
  END as TokenStatus,
  ExpiresAt,
  LastRefreshedAt
FROM ChurchSuiteCredentials;
```

---

## 🐛 Debugging with Browser DevTools

### Check Network Requests
1. Open browser DevTools: `F12`
2. Go to **Network** tab
3. Click "Sign In with ChurchSuite"
4. Look for requests to:
   - `https://login.churchsuite.com/oauth2/authorize` ✅ (redirect)
   - `https://localhost:5001/signin-churchsuite` ✅ (callback)

### Check Browser Storage
1. Open browser DevTools: `F12`
2. Go to **Application** tab
3. Click **Cookies** → select localhost
4. Look for `.AspNetCore.Session` cookie ✅

### Check Console Errors
1. Open browser DevTools: `F12`
2. Go to **Console** tab
3. Should be no errors ✅
4. May see warnings (normal)

---

## ✅ Checklist - Everything Working?

Complete these checks to verify everything is working:

- [ ] Application starts without errors
- [ ] Registration page loads with ChurchSuite button
- [ ] Clicking button redirects to ChurchSuite
- [ ] Can log in with ChurchSuite credentials
- [ ] Return to application successful
- [ ] User account created/linked
- [ ] Database has ChurchSuite credentials record
- [ ] AccessToken field is populated
- [ ] RefreshToken field is populated
- [ ] ExpiresAt has a future date
- [ ] IsActive is true
- [ ] Check auth endpoint returns correct status
- [ ] Token refresh works when expired
- [ ] No errors in application logs
- [ ] Session data created and cleaned up properly

**All checked?** 🎉 Implementation is working!

---

## 🆘 Quick Troubleshooting

| Problem | Quick Fix |
|---------|-----------|
| Button doesn't appear | Check UI page loaded correctly (F5 refresh) |
| Redirects to wrong URL | Verify callback URL in ChurchSuite OAuth app settings |
| "No ClientId configured" | Add to appsettings: `"ClientId": "your-id"` |
| Token never refreshes | Check `LastRefreshedAt` in database |
| Keeps asking to authorize | Clear browser cookies and try again |
| User not created after auth | Check AspNetUsers table for duplicates |
| Session expires mid-flow | Increase session timeout in Program.cs |

---

## 📝 Sample Test Report

Use this to document your test results:

```
Date: [Today's Date]
Tester: [Your Name]
Environment: Local Development

PKCE Flow Test:
[ ] Application started successfully
[ ] Sign In button visible
[ ] Redirected to ChurchSuite
[ ] Successfully authenticated
[ ] Returned to application
[ ] User account created/updated
[ ] Credentials stored in DB

Token Management Test:
[ ] Token stored with correct values
[ ] Token expiration date is in future
[ ] Refresh token is populated
[ ] Active flag is true

Refresh Flow Test:
[ ] Manually expired token
[ ] Triggered API call
[ ] Token refreshed automatically
[ ] LastRefreshedAt updated
[ ] API call succeeded with new token

Overall Status: [ ] PASS [ ] FAIL

Notes:
[Your observations]
```
