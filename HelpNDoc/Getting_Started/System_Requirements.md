System Requirements

Server requirements (recommended)

- .NET 8 runtime
- Windows Server 2022 or Linux distro with systemd for hosting
- SQL Server 2019+ or Azure SQL (managed)
- 2+ CPU cores, 4GB+ RAM for small deployments
- Blob storage or S3-compatible object store for uploaded files

Client requirements

- Modern browser (Edge, Chrome, Firefox, Safari) with JavaScript enabled
- Email client for verification links and notifications

Network

- Open outbound ports for SMTP and external OAuth providers
- TLS/HTTPS required for production

Notes

- Cloud deployments should use managed databases and blob storage for scalability and backups.
- For high-volume sites increase CPU/RAM and evaluate queueing for email delivery.