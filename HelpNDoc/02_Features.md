Features of ForestChurches

Overview

ForestChurches groups features by three main areas: Church management, Event management, and Communications. Each feature is aimed at lowering the overhead of coordination for local churches and volunteers.

Core features

- Multi-tenant support: Each church operates under its own tenant (data partition), with separate configuration and users.
- Church onboarding: Whitelisting + self-registration flow for approved churches.
- User roles & permissions: Roles for Church Admin, Event Organizer, Volunteer, Member, and System Administrator.
- Event creation and management: Create events, set capacity, registration forms, recurring schedules.
- Attendee management: Export lists, mark attendance, check-in support.
- Email communication: Send targeted group emails, templates, history and logs.
- Document uploads and attachments: Preserve original filenames when storing or sending attachments.
- Audit logging: Track administrative changes and important user actions.
- Security & compliance: Account-level protections and secure storage, no static client secrets for OAuth public flows.

Integrations

- OAuth for per-user external services (example: ChurchSuite via PKCE public-client flow)
- SMTP integration for outgoing email
- Optional webhook endpoints for external synchronization

Administrative utilities

- Admin dashboard for tenant and user management
- Bulk import/export of users and attendee lists
- Event reporting and simple analytics

This document set contains deeper articles for configuring and operating each of these features.