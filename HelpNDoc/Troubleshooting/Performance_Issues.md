Performance Issues

Symptoms

- Slow page loads, delayed email sending, or long-running background jobs.

Checks

- Database: check slow queries, indexing, and connection pool saturation.
- Background workers: backlog in queues; inspect queue length and worker health.
- Caching: ensure cache is configured for frequently-requested data.

Remediation

- Scale out web servers and database where appropriate.
- Optimize queries and add indexing for heavy-reporting queries.
- Throttle expensive background jobs and add progressive backoff.
