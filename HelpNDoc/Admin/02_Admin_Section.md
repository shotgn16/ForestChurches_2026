How the admin section works

Access and permissions

- Admin sign-in: Admins sign into the platform using their admin credentials. Global admins see an Admin Console link in the main navigation.
- Role separation: Admin roles are separate from tenant roles (e.g., PlatformAdmin, SupportAgent). Grant the minimum privileges required.

Admin Console structure

- Dashboard: Shows system health, pending tenant approvals, and recent audit events.
- Tenants: List of churches with quick actions (Approve, Suspend, Edit).
- Users: Global user search, cross-tenant actions (reset password, deactivate).
- Integrations: Configure OAuth providers, SMTP servers, and webhook endpoints.
- Logs & Audit: Searchable audit trail for platform-level actions.

Common admin workflows

- Approve a new church: Review request details, optionally edit tenant metadata, then approve to add to the whitelist.
- Emergency suspend: Temporarily suspend a tenant or user in response to abuse.
- Update global SMTP: Update credentials and test sending to confirm global email delivery.

Safety

- Use audit logs to confirm actions and to provide rollback guidance when needed.
- Limit the number of users with full platform admin rights and use scoped support roles for day-to-day tasks.

For integration changes that affect all tenants (e.g., OAuth provider changes), communicate scheduled maintenance and test with a staging tenant first.