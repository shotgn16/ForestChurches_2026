Architecture: Design, Hosting, and Communication

High-level design

ForestChurches is built as a multi-tenant Razor Pages web application on .NET 8. The application separates responsibilities into:

- Web UI (Razor Pages) — user-facing pages for churches, users and administrators.
- Server-side services — background workers, email dispatch, scheduled tasks.
- Data storage — relational database for tenants, users, events and logs.
- File storage — secure blob or file storage for uploaded documents (preserve original filenames).

Hosting

Typical hosting options:

- Cloud (recommended): Host in Azure App Service or a Linux-based app service container. Use Azure SQL or managed database and blob storage for files.
- On-premises: Deploy to IIS with a SQL Server backend and a secure file share or S3-compatible object store.

Security and tenancy

- Tenant isolation: All tenant-specific data is partitioned by TenantId in the database and via scoped services in code.
- Authentication: ASP.NET Core Identity or external OAuth providers per-user. Use PKCE / public client flow for 3rd-party integrations.
- Authorization: Role-based authorization at page and API endpoints (e.g., [Authorize(Roles = "ChurchAdmin")]).

Communication patterns

- Synchronous HTTP: UI requests served by Razor Page handlers.
- Background processing: Email sending and heavy work handled by background worker (IHostedService or queue-based worker).
- Email: SMTP or third-party email provider via secure credentials stored in config/KeyVault.
- Webhooks: Optional outgoing webhooks to notify external systems of event changes.

Extensibility

- Integration points: OAuth-based connectors and webhook endpoints.
- Configuration: Per-tenant settings stored in database; admins can configure locale, email templates, capacity defaults, and whitelisting rules.

Operational notes

- Logging: Application logs to centralized logging (App Insights or ELK) with structured events for critical actions.
- Backups: Implement regular DB and storage backups for tenant data and uploaded files.
- Monitoring: Health endpoints and background job metrics to monitor queue length and email throughput.

This page is intended for administrators and IT staff responsible for deploying and maintaining ForestChurches.