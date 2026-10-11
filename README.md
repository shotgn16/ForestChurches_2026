# 🌲 Forest Churches – Razor Pages Web Application

Welcome to the **Forest Churches** project!
This is a modern, secure, and scalable web application built with **ASP.NET Core Razor Pages (.NET 8, C# 12)**. It is designed for robust event management, secure administration, and seamless integration with cloud services.

[![Build, Test, and Deploy to Azure](https://github.com/shotgn16/forest_churches/actions/workflows/deploy.yml/badge.svg?branch=master)](https://github.com/shotgn16/forest_churches/actions/workflows/deploy.yml)

---

## 🚀 Features

- **Razor Pages Architecture**  
  Clean, maintainable, and fast web UI using ASP.NET Core Razor Pages.

- **Secure Authentication & Authorization**  
  - ASP.NET Core Identity integration (Areas/Identity)
  - Custom role management (SuperAdmin, others defined in Components/Roles)
  - Anti-forgery protection and secure cookie policies

- **Session Management**  
  - Idle/session handling implemented in Program.cs and Identity configuration
  - Strict SameSite and Secure cookie policies

- **Cloud Integration**  
  - Azure Key Vault for secrets (Configuration via ServiceRegistration/Program.cs)
  - Azure Blob Storage used for images / data protection keys (Components/Storage)

- **Background Processing**  
  - Hangfire for scheduled and recurring jobs (configured in Program.cs)
  - Jobs and background utilities under Components/Hangfire and Tools/

- **Logging & Error Handling**  
  - Structured logging and log retrieval under Components/Logging
  - Support file generation and server-side error handling; admin access to logs

- **Security Best Practices**  
  - HSTS, CSP, Referrer-Policy, X-Content-Type-Options, X-Frame-Options applied via middleware and headers
  - Custom middleware to block dangerous file downloads
  - Automatic database migrations and seeding (Migrations/ + seeders in Components/Configuration)

- **CORS Policy**  
  - Example policy restricts API access to configured origin(s) (see Program.cs / PolicyConfiguration)

- **ChurchSuite Integration**  
  - PKCE-first OAuth integration for per-user ChurchSuite connections (Pages/ChurchSuite/Authorize.cshtml.cs and Callback.cshtml.cs)
  - Centralized token service and API client (Components/Http/ChurchSuiteTokenService.cs and ChurchSuiteService.cs)

---

## ✨ Highlights

- **Enterprise-Grade Security** — anti-forgery, strict cookie policies, CSP, and more.
- **Cloud-Ready** — Azure Key Vault and Blob Storage integration.
- **Automated Event Management** — recurring sync jobs for ChurchSuite events and app-generated cleanup.
- **Robust Error Handling** — centralized logging, support file generation, and admin-only log access.
- **Easy Extensibility** — modular service registration, clear components, and Razor Pages views.

---

## 🛠️ Getting Started (local development)

1. Clone the repository:

```powershell
git clone https://github.com/shotgn16/ForestChurches_2026.git
cd "ForestChurches (19.10.2025)"
```

2. Configure environment & secrets:
- Use Azure Key Vault or local environment variables for secrets referenced in Program.cs and ServiceRegistration (seq-key, db connection strings, Key Vault settings, Hangfire credentials).
- If testing ChurchSuite OAuth, register an app in ChurchSuite and set the redirect URI to `https://localhost:5001/ChurchSuite/Callback` (or your host).

3. Database:
- Ensure your MySQL (or configured provider) database is available.
- Migrations are present under `Migrations/`; application will attempt to apply migrations on startup by default.

4. Run the app:

```powershell
dotnet run --project ForestChurches.csproj
```

5. Preview documentation locally:
- Open `docs/index.html` in a browser or serve the docs folder (e.g., `python -m http.server` from `docs/`).

6. Access Hangfire dashboard (if configured):
- Visit `/hangfire` on the running host. Credentials are read from Key Vault or configuration.

---

## 📁 Project Structure (high-level)

- `Program.cs` – Main entry point, services, middleware pipeline and feature registration
- `Components/` – Modular app components (Email, Logging, Roles, Http clients, Storage, etc.)
- `Pages/` – Razor Pages UI (Admin pages under `Pages/Admin`, ChurchSuite under `Pages/ChurchSuite`, Identity UI under `Areas/Identity`)
- `Data/` – EF Core context `ForestChurchesContext` and `Migrations/`
- `wwwroot/` – Static assets, JS helpers (e.g., `wwwroot/js/churchprofile.js`)

---

## 🔒 Security Notes

- Secrets are managed via Azure Key Vault in production; local development may use environment variables or user secrets.
- The app enforces safe headers and blocks unsafe downloads via middleware.
- Only users in privileged roles (SuperAdmin) can access application logs and some admin features.

---

## 📚 Documentation

- Human-readable site summary pages available under `docs/` (generated HTML). Open `docs/index.html` to view.
- More detailed developer/reference docs are in the `Documentation/` folder (many markdown guides including PKCE implementation and testing instructions).

---

Built with ❤️ using ASP.NET Core Razor Pages, .NET 8, and modern cloud integrations.
