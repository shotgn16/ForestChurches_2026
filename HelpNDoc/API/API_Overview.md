API Overview

ForestChurches may expose RESTful endpoints for integrations (subject to tenant permissions).

Capabilities

- Retrieve events, registrations, users, and tenant metadata
- Submit registrations or update attendee status
- Webhook support: receive event change notifications

Security

- Use OAuth 2.0 with PKCE for per-user integrations where possible.
- API keys for service accounts should be scoped and rotated regularly.

Rate limits

- API rate limits may apply; check platform admin settings for limit details.
