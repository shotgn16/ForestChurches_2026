Error Codes

General guidance

- 400 Bad Request: malformed payloads or validation errors.
- 401 Unauthorized: missing or invalid authentication token.
- 403 Forbidden: insufficient permissions for the operation.
- 404 Not Found: resource does not exist or not visible to tenant.
- 429 Too Many Requests: rate limit exceeded.
- 500 Internal Server Error: unexpected server error; check logs and trace ID.

Reporting

- When reporting errors, include the timestamp, trace ID (if available), user id, and the action attempted.
