Endpoint Descriptions

Common endpoints (examples)

- GET /api/v1/tenants/{tenantId}/events - list events
- GET /api/v1/events/{eventId}/registrations - list registrations
- POST /api/v1/events/{eventId}/register - create a registration
- GET /api/v1/users/{userId} - get user profile

Request/Response

- Endpoints use JSON payloads and standard HTTP response codes.
- Include Authorization header with Bearer token for protected endpoints.

Pagination & Filtering

- Use ?page= and ?pageSize= for list endpoints.
- Filter by date range, status, and tags where supported.

See SDKs and sample code for usage examples.
