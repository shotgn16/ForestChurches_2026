Installation Guide

Overview

This guide covers installing ForestChurches on a new server or cloud instance. It assumes you have a packaged deployment or source build targeting .NET 8.

1. Prerequisites
- Install .NET 8 runtime and SDK (if building locally).
- Provision a SQL database and storage account.
- Obtain SMTP credentials and any OAuth provider configuration.

2. Deploy the web app
- For Azure: deploy to App Service or Container Instance. Configure connection strings and app settings.
- For on-premises Windows: publish and host under IIS with an app pool running the appropriate .NET runtime.

3. Configure app settings
- ConnectionStrings: set database connection.
- Storage: set blob/file storage settings.
- Email: set SMTP host/username/password and test sending.
- Security: set up DataProtection keys and optional KeyVault secrets.

4. Run database migrations
- Execute EF Core migrations or run provided SQL scripts to create schema.

5. Start the app and verify
- Open the site using HTTPS. Create an initial admin user or follow provisioning instructions.

6. Post-install
- Configure backups and monitoring, and secure admin accounts with MFA.
