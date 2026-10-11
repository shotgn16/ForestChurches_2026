# Testing Summary - Choose Your Path

## 📋 Three Testing Options

### Option 1: Quick Manual Test (5 minutes) ⚡
**Best for**: Quick verification that basics work  
**When**: After initial implementation or small changes  
**Location**: See `QUICK_TEST_STEPS.md`

**What you'll do:**
1. Run the app
2. Click "Sign In with ChurchSuite"
3. Complete auth with real ChurchSuite
4. Verify credentials in database

---

### Option 2: Comprehensive Manual Testing (30 minutes) ✅
**Best for**: Full feature validation before deployment  
**When**: Before pushing to production  
**Location**: See `TESTING_GUIDE.md`

**What you'll test:**
- PKCE flow end-to-end
- Token refresh when expired
- Per-user ClientId fallback
- Error handling scenarios
- Session management
- API endpoint responses

---

### Option 3: Automated Unit Tests (45 minutes) 🤖
**Best for**: Regression prevention and CI/CD pipeline  
**When**: Before merging to main branch  
**Location**: See `AUTOMATED_TESTS.md`

**What you'll test:**
- Token service methods
- PKCE URL generation
- Session storage/retrieval
- Success/failure scenarios
- Edge cases

---

## 🚀 Quick Start - Do This First

### 1. **Run the Application**
```powershell
cd "C:\Users\JackBarnard\OneDrive - OptimiDoc s.r.o\Desktop\Other\ForestChurches (19.10.2025)"
dotnet run
```

### 2. **Open Browser**
```
https://localhost:5001/Identity/Account/Register
```

### 3. **Look for the Button**
You should see: **"Sign In with ChurchSuite"**

If you see it ✅ → Implementation is working!  
If you don't see it ❌ → Check browser console (F12) for errors

### 4. **Click & Complete Auth**
- Click the button
- Log in with real ChurchSuite credentials
- Authorize the app
- Return to your app

### 5. **Verify Success**
Check your database:
```sql
SELECT * FROM ChurchSuiteCredentials WHERE UserId = 'your-user-id' LIMIT 1;
```

Should show:
- ✅ `AccessToken` (not null)
- ✅ `RefreshToken` (not null)
- ✅ `ExpiresAt` (future date)
- ✅ `IsActive` (true)

---

## 📊 Testing Matrix

| Scenario | Manual | Automated | Notes |
|----------|--------|-----------|-------|
| PKCE flow | ✅ | ✅ | Most important - test first |
| Token refresh | ✅ | ✅ | Test expiry handling |
| Error handling | ✅ | ✅ | Missing config, network errors |
| Session management | ✅ | ⚠️ | Mock sessions in tests |
| Per-user ClientId | ✅ | ❌ | No automated test needed |
| API endpoints | ✅ | ✅ | Check `/churchsuite/check-auth` |
| Database integrity | ✅ | ❌ | Verify with SQL queries |

---

## 🎯 Success Criteria

Your implementation is successful when:

### Must Have ✅
- [ ] User can click "Sign In with ChurchSuite"
- [ ] Redirected to ChurchSuite login page
- [ ] Can authenticate with ChurchSuite
- [ ] Returns to application successfully
- [ ] Credentials stored in database with all fields populated
- [ ] User is authenticated/signed in

### Should Have ✅
- [ ] Expired tokens are automatically refreshed
- [ ] Token refresh works transparently (no user action)
- [ ] No hardcoded secrets in code/config
- [ ] Proper error handling with logging
- [ ] Session data is managed securely

### Nice to Have ✅
- [ ] Automated tests pass
- [ ] Per-user ClientId fallback works
- [ ] Authorization status API works
- [ ] Documentation is clear and complete

---

## 🔍 Where to Look for Issues

### Application Won't Start
```
Check: 
1. Build errors? Run: dotnet clean && dotnet build
2. Database connection? Check appsettings.json
3. Dependencies missing? Run: dotnet restore
```

### Button Not Appearing
```
Check:
1. Browser console (F12) for JavaScript errors
2. ExternalLogin.cshtml modified correctly?
3. Services registered in Program.cs?
4. CSS not hiding the button?
```

### Redirect to ChurchSuite Fails
```
Check:
1. ClientId configured? Check appsettings/Key Vault
2. Callback URL correct? Verify in ChurchSuite OAuth settings
3. Network connectivity? Can ping login.churchsuite.com?
```

### Token Exchange Fails
```
Check application logs for:
- "Token exchange failed" messages
- HTTP status codes (400, 401, 403, 500)
- Missing code_verifier
- Invalid client_id
```

### Credentials Not Stored
```
Check:
1. Database accessible? Run SQL query manually
2. ForestChurchesContext has ChurchSuiteCredentials DbSet?
3. User exists in AspNetUsers table?
4. No foreign key constraint errors in logs?
```

---

## 📝 Test Execution Checklist

### Before Testing
- [ ] Application builds successfully
- [ ] Database is accessible
- [ ] ChurchSuite OAuth app is configured
- [ ] ClientId is set in configuration
- [ ] Callback URL matches ChurchSuite settings

### During Testing
- [ ] Open browser DevTools (F12)
- [ ] Monitor Network tab for requests
- [ ] Check Application tab for session cookies
- [ ] Watch Console for JavaScript errors
- [ ] Monitor application logs

### After Testing
- [ ] Record all results (pass/fail)
- [ ] Screenshot key steps
- [ ] Document any issues found
- [ ] Note response times (normal < 5 seconds)
- [ ] Verify database changes

---

## 🆘 Quick Help

### "PKCE code_verifier not found in session"
→ Session expired - complete auth within 5 minutes

### "No ClientId configured"
→ Add `"ClientId": "..."` to appsettings.json

### "User not found"
→ Check user exists in AspNetUsers table

### Build fails after changes
→ Run: `dotnet clean && dotnet build`

### Tests fail
→ Check mocks are set up correctly, use `--verbosity detailed`

---

## 📚 Documentation Files

Your implementation includes:

1. **CHURCHSUITE_PKCE_IMPLEMENTATION.md**
   - Architecture overview
   - File structure
   - Configuration details

2. **TESTING_GUIDE.md**
   - Comprehensive testing procedures
   - Unit test examples
   - Integration test examples
   - Troubleshooting guide

3. **QUICK_TEST_STEPS.md**
   - Step-by-step visual guide
   - 5-minute quick test
   - Database SQL commands
   - Checklist format

4. **AUTOMATED_TESTS.md**
   - Test project setup
   - Example test code
   - How to run tests
   - CI/CD integration

5. **TESTING_SUMMARY.md** (this file)
   - Overview of all testing options
   - Quick start guide
   - Success criteria

---

## 🎓 Learning Path

### If you're new to this codebase:
1. Read: `CHURCHSUITE_PKCE_IMPLEMENTATION.md` (understand architecture)
2. Do: `QUICK_TEST_STEPS.md` (see it working)
3. Read: `TESTING_GUIDE.md` (understand test scenarios)
4. Try: `AUTOMATED_TESTS.md` (learn testing patterns)

### If you're fixing a bug:
1. Identify which component has the bug
2. Check corresponding test in `AUTOMATED_TESTS.md`
3. Run that specific test to confirm failure
4. Fix the code
5. Run test again to confirm success

### If you're adding a feature:
1. Add test first (TDD style)
2. Implement feature
3. Run test to verify
4. Run all tests to ensure no regression
5. Update documentation

---

## ✨ Next Steps After Testing

1. ✅ **All tests pass?**
   - Commit code to git
   - Push to development branch
   - Create pull request

2. ✅ **Deploy to staging?**
   - Verify in staging environment
   - Test with real ChurchSuite account
   - Load test with multiple users

3. ✅ **Ready for production?**
   - Get approval from team lead
   - Set up monitoring/alerts
   - Document rollback procedure
   - Deploy with confidence

4. ✅ **Post-deployment?**
   - Monitor logs for errors
   - Check token refresh patterns
   - Gather user feedback
   - Plan for future improvements

---

## 📞 Support

Need help? Check these files in order:

1. **This file** - For testing overview and quick solutions
2. **QUICK_TEST_STEPS.md** - For step-by-step instructions
3. **TESTING_GUIDE.md** - For detailed scenarios and debugging
4. **CHURCHSUITE_PKCE_IMPLEMENTATION.md** - For technical details
5. **AUTOMATED_TESTS.md** - For testing code patterns

---

**Ready to test?** Start with: `QUICK_TEST_STEPS.md`

Good luck! 🚀
