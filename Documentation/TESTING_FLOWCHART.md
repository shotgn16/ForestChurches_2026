# Testing Strategy Flowchart

## Decision Tree: Which Test Should I Run?

```
START HERE
	|
	v
How much time do you have?
	|
	├─ 5 minutes?      → QUICK MANUAL TEST
	|                   └─ See: QUICK_TEST_STEPS.md
	|
	├─ 30 minutes?     → COMPREHENSIVE MANUAL TEST  
	|                   └─ See: TESTING_GUIDE.md
	|
	└─ 1+ hours?       → FULL SUITE (MANUAL + AUTOMATED)
						├─ Manual first: TESTING_GUIDE.md
						└─ Then automated: AUTOMATED_TESTS.md
```

---

## PKCE Authentication Test Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  START: User clicks "Sign In with ChurchSuite"                 │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  ChurchsuiteController.LoginPKCE()                              │
│  - Generates PKCE code_challenge (S256)                         │
│  - Stores code_verifier in session                              │
│  - Generates unique state value                                 │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Verify PKCE values generated                          │
│  ✅ TEST: Verify session has verifier                           │
│  ✅ TEST: Verify state in session                               │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  User redirected to ChurchSuite login                           │
│  URL: https://login.churchsuite.com/oauth2/authorize?          │
│       client_id=XXX&code_challenge=YYY&state=ZZZ               │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Verify redirect URL has code_challenge                │
│  ✅ TEST: Verify redirect URL has S256 method                   │
│  ✅ TEST: Verify state value in URL                             │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  User authenticates with ChurchSuite                            │
│  (Manual step - enter credentials)                              │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Ensure your ChurchSuite account exists                │
│  ✅ TEST: Ensure OAuth app is authorized                        │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  User redirected back to your app                               │
│  URL: https://localhost:5001/signin-churchsuite?               │
│       code=AUTH_CODE&state=STATE_VALUE                          │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Verify callback received                              │
│  ✅ TEST: Check browser shows your app domain                   │
│  ✅ TEST: Verify code parameter present                         │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  OAuthCallbackController.SignInChurchSuite()                    │
│  - Retrieves code_verifier from session (using state)           │
│  - Retrieves ClientId (global or per-user)                      │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Verify code_verifier retrieved                        │
│  ✅ TEST: Verify correct ClientId selected                      │
│  ✅ TEST: Verify state matching works                           │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  ExchangeCodeForTokenAsync()                                    │
│  - POST to https://login.churchsuite.com/oauth2/token          │
│  - Send: code, code_verifier, client_id, redirect_uri          │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Verify POST request sent                              │
│  ✅ TEST: Verify all parameters included                        │
│  ✅ TEST: Check response status code (should be 200)            │
│  ✅ TEST: Verify response contains access_token                 │
│  ✅ TEST: Verify response contains refresh_token                │
│  ✅ TEST: Verify expires_in value received                      │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  ChurchSuiteTokenService.StoreCredentialsAsync()                │
│  - Save to ChurchSuiteCredentials table                         │
│  - Set ExpiresAt = now + expires_in seconds                     │
│  - Set IsActive = true                                          │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Query database for new record                         │
│  ✅ TEST: Verify AccessToken is stored                          │
│  ✅ TEST: Verify RefreshToken is stored                         │
│  ✅ TEST: Verify ExpiresAt is future date                       │
│  ✅ TEST: Verify IsActive = true                                │
│  ✅ TEST: Verify CreatedAt is recent                            │
│  ✅ TEST: Verify UserId matches logged-in user                  │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  PKCE session data cleared                                      │
│  - Remove code_verifier from session                            │
│  - Remove state from session                                    │
│  - Remove user_id from session                                  │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST: Verify session keys are removed                       │
│  ✅ TEST: Verify no stale PKCE data remains                     │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  Check if user already authenticated                            │
│  - If YES: Store credentials for that user                      │
│  - If NO: Create external login session for registration        │
├─────────────────────────────────────────────────────────────────┤
│  ✅ TEST (Existing User):                                       │
│  │   - Should be signed in already                              │
│  │   - Credentials updated in database                          │
│  │   - Redirected to home page                                  │
│  │                                                               │
│  ✅ TEST (New User):                                            │
│      - Created external login session                           │
│      - Redirected to registration page                          │
│      - Can complete registration                                │
└─────────────────────────────────────────────────────────────────┘
							│
							v
┌─────────────────────────────────────────────────────────────────┐
│  SUCCESS ✅                                                      │
│  User is authenticated and ChurchSuite credentials stored       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Token Refresh Test Flow

```
SCENARIO: API call happens, token is expired

START
  │
  ├─ Get valid access token
  │  └─ Query ChurchSuiteCredentials for user
  │     └─ Check ExpiresAt < now?
  │        │
  │        ├─ NO (still valid)
  │        │  └─ Use existing token ✅
  │        │
  │        └─ YES (expired)
  │           └─ Attempt refresh
  │              │
  │              ├─ Has refresh_token?
  │              │  │
  │              │  ├─ YES
  │              │  │  └─ POST to token endpoint
  │              │  │     └─ Wait for response
  │              │  │        │
  │              │  │        ├─ Success (200)
  │              │  │        │  └─ Update database
  │              │  │        │     └─ Use new token ✅
  │              │  │        │
  │              │  │        └─ Failure (401/400)
  │              │  │           └─ Return NULL ❌
  │              │  │
  │              │  └─ NO
  │              │     └─ Return NULL ❌
  │              │
  │              └─ Return token or NULL
  │
  ├─ Check token result
  │  │
  │  ├─ Token exists ✅
  │  │  └─ Make API call with token
  │  │
  │  └─ Token is NULL ❌
  │     └─ Prompt user to authorize
  │
  END
```

---

## Test Execution Path

```
Choose Test Type
	  │
	  ├─────────────────────────────────────────┐
	  │                                         │
	  v                                         v
  MANUAL TEST                           AUTOMATED TEST
  (Interactive)                         (Code-based)
	  │                                         │
	  ├─ Start app                             ├─ Create test project
	  ├─ Click button                          ├─ Mock dependencies
	  ├─ Complete auth                         ├─ Write test cases
	  ├─ Check database                        ├─ Run tests
	  ├─ Verify results                        └─ View coverage
	  └─ Document findings
	  │
	  v
  COMPARE RESULTS
	  │
	  ├─ Both pass? ✅ → Code is ready
	  ├─ One fails? ⚠️ → Investigate difference
	  └─ Both fail? ❌ → Fix the code
	  │
	  v
  COMMIT & PUSH
	  │
	  v
  CI/CD RUNS TESTS
	  │
	  ├─ Pass ✅ → PR can merge
	  └─ Fail ❌ → Fix before merge
```

---

## File Reference Guide

```
TESTING DOCUMENTS
	│
	├─ TESTING_SUMMARY.md (START HERE)
	│  └─ Decision tree and overview
	│
	├─ QUICK_TEST_STEPS.md (5 minutes)
	│  ├─ Run the app
	│  ├─ Click button
	│  ├─ Verify in database
	│  └─ Quick troubleshooting
	│
	├─ TESTING_GUIDE.md (30 minutes)
	│  ├─ PKCE flow test
	│  ├─ Token refresh test
	│  ├─ Per-user ClientId test
	│  ├─ Endpoint tests
	│  └─ Session tests
	│
	└─ AUTOMATED_TESTS.md (1+ hours)
	   ├─ Set up test project
	   ├─ Write unit tests
	   ├─ Write integration tests
	   ├─ Run test suite
	   └─ View coverage

IMPLEMENTATION DOCUMENTS
	│
	├─ CHURCHSUITE_PKCE_IMPLEMENTATION.md
	│  ├─ Architecture
	│  ├─ File structure
	│  ├─ Configuration
	│  ├─ Database schema
	│  └─ User flows
	│
	└─ Code files
	   ├─ Components/Http/ChurchSuiteTokenService.cs
	   ├─ Components/Http/ChurchSuiteAuthenticationService.cs
	   ├─ Controllers/ChurchsuiteController.cs
	   ├─ Controllers/OAuthCallbackController.cs
	   └─ ...
```

---

## Success Criteria Checklist

```
BASIC FUNCTIONALITY
	[ ] App starts without errors
	[ ] Sign In button appears
	[ ] Button redirects to ChurchSuite
	[ ] Can log in with ChurchSuite
	[ ] Returns to your app
	[ ] User is created/signed in

DATABASE
	[ ] ChurchSuiteCredentials record created
	[ ] AccessToken populated
	[ ] RefreshToken populated
	[ ] ExpiresAt is future date
	[ ] IsActive is true
	[ ] UserId matches logged-in user

API ENDPOINTS
	[ ] /churchsuite/login-pkce works
	[ ] /churchsuite/check-auth works
	[ ] /signin-churchsuite handles callback

REFRESH LOGIC
	[ ] Expired tokens are refreshed
	[ ] LastRefreshedAt is updated
	[ ] API calls work after refresh
	[ ] No user interaction required

ERROR HANDLING
	[ ] Missing ClientId → error message
	[ ] Invalid code → redirect to login
	[ ] Refresh fails → redirect to authorize
	[ ] Network error → graceful handling
	[ ] All errors logged

SECURITY
	[ ] No hardcoded secrets
	[ ] PKCE code_verifier in session (not URL)
	[ ] State value for CSRF protection
	[ ] SameSite cookies configured
	[ ] Session timeout reasonable
	[ ] Credentials encrypted in transit

TESTS (If Automated)
	[ ] All unit tests pass
	[ ] All integration tests pass
	[ ] Code coverage > 80%
	[ ] No warnings or deprecated code
	[ ] Tests run in CI/CD pipeline
```

---

## Time Estimates

| Task | Time | Difficulty |
|------|------|-----------|
| Quick manual test | 5 min | ⭐ Easy |
| Comprehensive manual test | 30 min | ⭐⭐ Medium |
| Set up automated tests | 45 min | ⭐⭐ Medium |
| Write 5 unit tests | 1 hour | ⭐⭐⭐ Hard |
| Full suite (manual + automated) | 2 hours | ⭐⭐⭐ Hard |

---

**Ready to start?** Pick your test path above and follow the corresponding document!
