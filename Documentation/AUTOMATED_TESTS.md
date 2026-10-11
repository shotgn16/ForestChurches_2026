# Automated Test Examples

## Setup Test Project

### 1. Create Test Project
```powershell
cd "C:\Users\JackBarnard\OneDrive - OptimiDoc s.r.o\Desktop\Other\ForestChurches (19.10.2025)"
dotnet new xunit -n ForestChurches.Tests
cd ForestChurches.Tests
dotnet add reference ../ForestChurches.csproj
dotnet add package Moq
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

### 2. Create Directory Structure
```
ForestChurches.Tests/
├── Components/
│   └── Http/
│       ├── ChurchSuiteTokenServiceTests.cs
│       └── ChurchSuiteAuthenticationServiceTests.cs
├── Controllers/
│   └── OAuthCallbackControllerTests.cs
└── Integration/
	└── PKCEFlowTests.cs
```

---

## Test 1: ChurchSuiteTokenService - GetValidAccessTokenAsync

File: `ForestChurches.Tests/Components/Http/ChurchSuiteTokenServiceTests.cs`

```csharp
using Xunit;
using Moq;
using ForestChurches.Components.Http;
using ForestChurches.Components.Configuration;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;

public class ChurchSuiteTokenServiceTests
{
	private Mock<ForestChurchesContext> _mockContext;
	private Mock<UserManager<ChurchAccount>> _mockUserManager;
	private Mock<ILogger<ChurchSuiteTokenService>> _mockLogger;
	private Mock<IOptions<ChurchSuiteOAuthOptions>> _mockOptions;
	private Mock<IHttpClientFactory> _mockHttpFactory;

	public ChurchSuiteTokenServiceTests()
	{
		_mockContext = new Mock<ForestChurchesContext>();
		_mockUserManager = new Mock<UserManager<ChurchAccount>>(
			new Mock<IUserStore<ChurchAccount>>().Object, null, null, null, null, null, null, null, null);
		_mockLogger = new Mock<ILogger<ChurchSuiteTokenService>>();
		_mockOptions = new Mock<IOptions<ChurchSuiteOAuthOptions>>();
		_mockHttpFactory = new Mock<IHttpClientFactory>();
	}

	[Fact]
	public async Task GetValidAccessTokenAsync_WithValidToken_ReturnsToken()
	{
		// Arrange
		var userId = "user-123";
		var validToken = "valid-access-token";

		var credential = new ChurchSuiteCredential
		{
			Id = 1,
			UserId = userId,
			AccessToken = validToken,
			ExpiresAt = DateTime.UtcNow.AddHours(1), // Not expired
			IsActive = true,
			Subdomain = "test"
		};

		var mockDbSet = new Mock<DbSet<ChurchSuiteCredential>>();
		var credentials = new[] { credential }.AsQueryable();

		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Provider)
			.Returns(credentials.Provider);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Expression)
			.Returns(credentials.Expression);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.ElementType)
			.Returns(credentials.ElementType);

		_mockContext.Setup(c => c.ChurchSuiteCredentials)
			.Returns(mockDbSet.Object);

		var service = new ChurchSuiteTokenService(
			_mockContext.Object,
			_mockUserManager.Object,
			_mockLogger.Object,
			_mockOptions.Object,
			_mockHttpFactory.Object);

		// Act
		var result = await service.GetValidAccessTokenAsync(userId);

		// Assert
		Assert.NotNull(result);
		Assert.Equal(validToken, result);
	}

	[Fact]
	public async Task GetValidAccessTokenAsync_WithExpiredToken_ReturnsNull()
	{
		// Arrange
		var userId = "user-123";
		var expiredToken = "expired-token";

		var credential = new ChurchSuiteCredential
		{
			Id = 1,
			UserId = userId,
			AccessToken = expiredToken,
			RefreshToken = null,
			ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
			IsActive = true,
			Subdomain = "test"
		};

		var mockDbSet = new Mock<DbSet<ChurchSuiteCredential>>();
		var credentials = new[] { credential }.AsQueryable();

		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Provider)
			.Returns(credentials.Provider);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Expression)
			.Returns(credentials.Expression);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.ElementType)
			.Returns(credentials.ElementType);

		_mockContext.Setup(c => c.ChurchSuiteCredentials)
			.Returns(mockDbSet.Object);

		var service = new ChurchSuiteTokenService(
			_mockContext.Object,
			_mockUserManager.Object,
			_mockLogger.Object,
			_mockOptions.Object,
			_mockHttpFactory.Object);

		// Act
		var result = await service.GetValidAccessTokenAsync(userId);

		// Assert
		Assert.Null(result);
	}

	[Fact]
	public async Task HasValidTokenAsync_WithValidToken_ReturnsTrue()
	{
		// Arrange
		var userId = "user-123";
		var credential = new ChurchSuiteCredential
		{
			UserId = userId,
			AccessToken = "token",
			ExpiresAt = DateTime.UtcNow.AddHours(1),
			IsActive = true,
			Subdomain = "test"
		};

		var mockDbSet = new Mock<DbSet<ChurchSuiteCredential>>();
		var credentials = new[] { credential }.AsQueryable();

		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Provider)
			.Returns(credentials.Provider);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Expression)
			.Returns(credentials.Expression);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.ElementType)
			.Returns(credentials.ElementType);

		_mockContext.Setup(c => c.ChurchSuiteCredentials)
			.Returns(mockDbSet.Object);

		var service = new ChurchSuiteTokenService(
			_mockContext.Object,
			_mockUserManager.Object,
			_mockLogger.Object,
			_mockOptions.Object,
			_mockHttpFactory.Object);

		// Act
		var result = await service.HasValidTokenAsync(userId);

		// Assert
		Assert.True(result);
	}

	[Fact]
	public async Task HasValidTokenAsync_WithExpiredToken_ReturnsFalse()
	{
		// Arrange
		var userId = "user-123";
		var credential = new ChurchSuiteCredential
		{
			UserId = userId,
			AccessToken = "token",
			ExpiresAt = DateTime.UtcNow.AddHours(-1), // Expired
			IsActive = true,
			Subdomain = "test"
		};

		var mockDbSet = new Mock<DbSet<ChurchSuiteCredential>>();
		var credentials = new[] { credential }.AsQueryable();

		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Provider)
			.Returns(credentials.Provider);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.Expression)
			.Returns(credentials.Expression);
		mockDbSet.As<IQueryable<ChurchSuiteCredential>>()
			.Setup(m => m.ElementType)
			.Returns(credentials.ElementType);

		_mockContext.Setup(c => c.ChurchSuiteCredentials)
			.Returns(mockDbSet.Object);

		var service = new ChurchSuiteTokenService(
			_mockContext.Object,
			_mockUserManager.Object,
			_mockLogger.Object,
			_mockOptions.Object,
			_mockHttpFactory.Object);

		// Act
		var result = await service.HasValidTokenAsync(userId);

		// Assert
		Assert.False(result);
	}
}
```

---

## Test 2: ChurchSuiteAuthenticationService - PKCE Generation

File: `ForestChurches.Tests/Components/Http/ChurchSuiteAuthenticationServiceTests.cs`

```csharp
using Xunit;
using Moq;
using ForestChurches.Components.Http;
using ForestChurches.Components.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

public class ChurchSuiteAuthenticationServiceTests
{
	[Fact]
	public async Task GeneratePKCEAuthorizationUrlAsync_GeneratesValidUrl()
	{
		// Arrange
		var mockHttpContext = new Mock<HttpContext>();
		var mockSession = new Mock<ISession>();
		mockHttpContext.Setup(h => h.Session).Returns(mockSession.Object);

		var mockLogger = new Mock<ILogger<ChurchSuiteAuthenticationService>>();

		var options = new ChurchSuiteOAuthOptions
		{
			ClientId = "test-client-id",
			AuthorizeEndpoint = "https://login.churchsuite.com/oauth2/authorize",
			RedirectUri = "https://localhost:5001/signin-churchsuite"
		};

		var mockOptions = new Mock<IOptions<ChurchSuiteOAuthOptions>>();
		mockOptions.Setup(o => o.Value).Returns(options);

		var service = new ChurchSuiteAuthenticationService(mockLogger.Object, mockOptions.Object);

		// Act
		var authUrl = await service.GeneratePKCEAuthorizationUrlAsync(
			mockHttpContext.Object,
			redirectUri: "https://localhost:5001/home");

		// Assert
		Assert.NotNull(authUrl);
		Assert.Contains("client_id=test-client-id", authUrl);
		Assert.Contains("code_challenge=", authUrl);
		Assert.Contains("code_challenge_method=S256", authUrl);
		Assert.Contains("state=", authUrl);
		Assert.Contains("response_type=code", authUrl);
	}

	[Fact]
	public void StorePKCEVerifierInSession_StoresVerifierSuccessfully()
	{
		// Arrange
		var mockHttpContext = new Mock<HttpContext>();
		var storedData = new System.Collections.Generic.Dictionary<string, byte[]>();

		var mockSession = new Mock<ISession>();
		mockSession.Setup(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()))
			.Callback<string, byte[]>((key, value) => storedData[key] = value);

		mockHttpContext.Setup(h => h.Session).Returns(mockSession.Object);

		var mockLogger = new Mock<ILogger<ChurchSuiteAuthenticationService>>();
		var mockOptions = new Mock<IOptions<ChurchSuiteOAuthOptions>>();

		var service = new ChurchSuiteAuthenticationService(mockLogger.Object, mockOptions.Object);

		var state = "test-state-123";
		var verifier = "test-verifier-long-string";
		var userId = "user-id";

		// Act
		service.StorePKCEVerifierInSession(mockHttpContext.Object, state, verifier, userId);

		// Assert
		Assert.NotEmpty(storedData);
		mockSession.Verify(s => s.Set(It.IsAny<string>(), It.IsAny<byte[]>()), Times.AtLeastOnce);
	}

	[Fact]
	public void GetPKCEVerifierFromSession_ReturnsStoredVerifier()
	{
		// Arrange
		var state = "test-state-123";
		var verifier = "test-verifier-long-string";

		var mockSession = new Mock<ISession>();
		mockSession.Setup(s => s.TryGetValue(
			It.IsAny<string>(), out It.Ref<byte[]>.IsAny))
			.Returns((string key, out byte[] value) =>
			{
				value = System.Text.Encoding.UTF8.GetBytes(verifier);
				return key.Contains("verifier");
			});

		var mockHttpContext = new Mock<HttpContext>();
		mockHttpContext.Setup(h => h.Session).Returns(mockSession.Object);

		var mockLogger = new Mock<ILogger<ChurchSuiteAuthenticationService>>();
		var mockOptions = new Mock<IOptions<ChurchSuiteOAuthOptions>>();

		var service = new ChurchSuiteAuthenticationService(mockLogger.Object, mockOptions.Object);

		// Act
		var result = service.GetPKCEVerifierFromSession(mockHttpContext.Object, state);

		// Assert
		Assert.NotNull(result);
	}
}
```

---

## Test 3: Run Tests

```powershell
cd ForestChurches.Tests
dotnet test --verbosity normal
```

Expected output:
```
Test Run Successful.
Total tests: 6
	 Passed: 6
	 Failed: 0
Skipped: 0
```

---

## Test 4: Run with Coverage

```powershell
# Install coverage tools
dotnet add package coverlet.collector

# Run tests with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover

# View coverage report
# Open: coverage.opencover.xml in your browser
```

---

## Running Tests in Visual Studio

1. **Open Test Explorer**
   - View → Test Explorer
   - Or press: `Ctrl + E, T`

2. **Run All Tests**
   - Click "Run All Tests" button
   - Or press: `Ctrl + R, A`

3. **Run Single Test**
   - Right-click test
   - Select "Run"
   - Or press: `Ctrl + R, T`

4. **Debug Test**
   - Right-click test
   - Select "Debug"
   - Or press: `Ctrl + R, Ctrl + T`

---

## Continuous Integration (GitHub Actions)

Create: `.github/workflows/test.yml`

```yaml
name: Tests

on:
  push:
	branches: [ master, develop ]
  pull_request:
	branches: [ master, develop ]

jobs:
  test:
	runs-on: ubuntu-latest

	steps:
	- uses: actions/checkout@v3

	- name: Setup .NET
	  uses: actions/setup-dotnet@v3
	  with:
		dotnet-version: '8.0.x'

	- name: Restore dependencies
	  run: dotnet restore

	- name: Build
	  run: dotnet build --no-restore

	- name: Run tests
	  run: dotnet test --no-build --verbosity normal --logger "trx;LogFileName=test-results.trx"

	- name: Upload test results
	  uses: actions/upload-artifact@v3
	  if: always()
	  with:
		name: test-results
		path: '**/test-results.trx'
```

---

## Troubleshooting Tests

| Issue | Solution |
|-------|----------|
| Tests can't find ForestChurches assembly | Run `dotnet add reference ../ForestChurches.csproj` |
| Mock DbSet not working | Use `AsQueryable()` on data |
| Tests are slow | Use Arrange-Act-Assert pattern cleanly |
| Database errors in tests | Use in-memory database or mocks |
| Session tests fail | Mock ISession properly |
| HTTP client tests fail | Mock IHttpClientFactory |

---

## Example: Running Single Test from Command Line

```powershell
# Run specific test class
dotnet test --filter "FullyQualifiedName~ChurchSuiteTokenServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~ChurchSuiteTokenServiceTests.GetValidAccessTokenAsync_WithValidToken_ReturnsToken"

# Run tests matching pattern
dotnet test --filter "Name~PKCE"
```
