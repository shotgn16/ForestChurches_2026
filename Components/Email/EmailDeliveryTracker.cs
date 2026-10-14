using System.Collections.Concurrent;

namespace ForestChurches.Components.Email
{
    /// <summary>Forest Churches' side of an email hand-over to the email app (api.forestchurches.co.uk).</summary>
    public enum EmailHandOverStage
    {
        /// <summary>Being posted to the email app (includes retries).</summary>
        Sending,
        /// <summary>The email app replied 2xx: it has received and queued the email.</summary>
        Accepted,
        /// <summary>Every attempt failed: the email app never received it.</summary>
        Failed
    }

    /// <summary>One email this app asked the email app to send.</summary>
    public class EmailDeliveryRecord
    {
        public string Id { get; init; } = string.Empty;
        public string Recipient { get; init; } = string.Empty;
        public string Subject { get; init; } = string.Empty;
        public string Template { get; init; } = string.Empty;
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        public EmailHandOverStage Stage { get; set; } = EmailHandOverStage.Sending;
        public int Attempts { get; set; }
        public int? LastHttpStatus { get; set; }
        public string? LastError { get; set; }
        public DateTime? AcceptedUtc { get; set; }

        /// <summary>Status reported by the email app (Queued, Sending, Retrying, Sent, Failed, Unknown).</summary>
        public string? RemoteStatus { get; set; }
        public int? RemoteAttempts { get; set; }
        public DateTime? RemoteSentUtc { get; set; }
        public string? RemoteError { get; set; }
        public DateTime? RemoteCheckedUtc { get; set; }

        /// <summary>True once nothing further will change: sent, or failed for good.</summary>
        public bool IsFinal =>
            Stage == EmailHandOverStage.Failed ||
            string.Equals(RemoteStatus, "Sent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(RemoteStatus, "Failed", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Remembers the most recent emails (newest 500) so site admins can see whether each one reached the
    /// email app and whether it has gone out. Held in memory and reset when the site restarts; every step
    /// is also written to the logs with the same EmailId, so older emails can be found under Admin → Logs.
    /// Registered as a singleton.
    /// </summary>
    public class EmailDeliveryTracker
    {
        private const int MaxEntries = 500;
        private readonly ConcurrentDictionary<string, EmailDeliveryRecord> _records = new(StringComparer.OrdinalIgnoreCase);

        public DateTime TrackingSinceUtc { get; } = DateTime.UtcNow;

        public EmailDeliveryRecord Start(string id, string recipient, string subject, string template)
        {
            var record = new EmailDeliveryRecord
            {
                Id = id,
                Recipient = recipient ?? string.Empty,
                Subject = subject ?? string.Empty,
                Template = template ?? string.Empty
            };
            _records[id] = record;

            var extra = _records.Count - MaxEntries;
            if (extra > 0)
            {
                foreach (var oldest in _records.Values.OrderBy(r => r.CreatedUtc).Take(extra).ToList())
                {
                    _records.TryRemove(oldest.Id, out _);
                }
            }

            return record;
        }

        public void Update(string id, Action<EmailDeliveryRecord> change)
        {
            if (_records.TryGetValue(id, out var record))
            {
                lock (record) { change(record); }
            }
        }

        public EmailDeliveryRecord? Get(string id) => _records.TryGetValue(id, out var record) ? record : null;

        public IReadOnlyList<EmailDeliveryRecord> Recent(int take = 200) =>
            _records.Values.OrderByDescending(r => r.CreatedUtc).Take(take).ToList();
    }
}
