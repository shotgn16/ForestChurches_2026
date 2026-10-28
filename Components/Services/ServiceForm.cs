using System.Globalization;
using ForestChurches.Models;

namespace ForestChurches.Components.Services
{
    /// <summary>
    /// What the "Add / edit a service" form posts. Kept separate from <see cref="ServiceTimes"/> so the form
    /// can use friendly shapes (checkbox lists, "where" choices, rows of dates) and so nothing the browser
    /// sends can set the owning church directly. Validation is done by <see cref="ServiceSchedule.Validate"/>.
    /// </summary>
    public class ServiceForm
    {
        /// <summary>Encrypted ID of the service being edited; empty when adding.</summary>
        public string? Id { get; set; }

        public string? Name { get; set; }
        public string? Time { get; set; }
        public string? EndTime { get; set; }

        public string RepeatType { get; set; } = ServiceRepeat.Weekly;
        public int WeeklyInterval { get; set; } = 1;
        public int MonthlyInterval { get; set; } = 1;
        public int DayOfWeek { get; set; } = 0;
        public List<int> WeeksOfMonth { get; set; } = new();
        public int? DayOfMonth { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public List<ServiceDateForm> Dates { get; set; } = new();

        /// <summary>Off: at its own church. On: at <see cref="LocationChurchId"/> or <see cref="LocationAddress"/>.</summary>
        public bool UseCustomLocation { get; set; }

        /// <summary>"church" (another church on the account) or "address".</summary>
        public string LocationKind { get; set; } = "church";

        /// <summary>Encrypted ID of another church on the same account.</summary>
        public string? LocationChurchId { get; set; }
        public string? LocationAddress { get; set; }

        public bool IsEdit => !string.IsNullOrEmpty(Id);

        /// <summary>Fill the form from a stored service (for editing).</summary>
        public static ServiceForm From(ServiceTimes s, Func<string, string> protectChurchId, Func<string, string> protectServiceId)
        {
            var monthly = s.RepeatType is ServiceRepeat.MonthlyByWeekday or ServiceRepeat.MonthlyByDate;
            return new ServiceForm
            {
                Id = protectServiceId(s.Id),
                Name = s.Note,
                Time = ServiceSchedule.ParseTime(s.Time)?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? s.Time,
                EndTime = ServiceSchedule.ParseTime(s.EndTime)?.ToString("HH:mm", CultureInfo.InvariantCulture),
                RepeatType = ServiceRepeat.All.Contains(s.RepeatType) ? s.RepeatType : ServiceRepeat.Weekly,
                WeeklyInterval = monthly ? 1 : Math.Max(1, s.Interval),
                MonthlyInterval = monthly ? Math.Max(1, s.Interval) : 1,
                DayOfWeek = s.DayOfWeek,
                WeeksOfMonth = ServiceSchedule.ParseWeeks(s.WeeksOfMonth).ToList(),
                DayOfMonth = s.DayOfMonth,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Dates = (s.Dates ?? new List<ServiceDate>()).OrderBy(d => d.Date)
                    .Select(d => new ServiceDateForm { Date = d.Date, Time = d.Time }).ToList(),
                UseCustomLocation = s.UseCustomLocation,
                LocationKind = string.IsNullOrEmpty(s.LocationChurchId) ? "address" : "church",
                LocationChurchId = string.IsNullOrEmpty(s.LocationChurchId) ? null : protectChurchId(s.LocationChurchId),
                LocationAddress = s.LocationAddress,
            };
        }

        /// <summary>
        /// Copy the form onto a service. <paramref name="locationChurchId"/> is the already-checked plain ID of
        /// the "held at" church (or null). Unused fields for the chosen repeat type are cleared so the stored
        /// row only describes one schedule.
        /// </summary>
        public void ApplyTo(ServiceTimes s, string? locationChurchId)
        {
            s.Note = (Name ?? string.Empty).Trim();
            s.Time = Normalise(Time) ?? (Time ?? string.Empty).Trim();
            s.EndTime = Normalise(EndTime);
            s.RepeatType = ServiceRepeat.All.Contains(RepeatType) ? RepeatType : ServiceRepeat.Weekly;
            s.StartDate = StartDate;
            s.EndDate = EndDate;
            s.DayOfWeek = Math.Clamp(DayOfWeek, 0, 6);
            s.WeeksOfMonth = null;
            s.DayOfMonth = null;
            s.Interval = 1;

            switch (s.RepeatType)
            {
                case ServiceRepeat.Weekly:
                    s.Interval = Math.Clamp(WeeklyInterval, 1, 52);
                    break;
                case ServiceRepeat.MonthlyByWeekday:
                    s.Interval = Math.Clamp(MonthlyInterval, 1, 12);
                    s.WeeksOfMonth = string.Join(",", (WeeksOfMonth ?? new List<int>()).Where(w => w is >= 1 and <= 5 or -1).Distinct());
                    break;
                case ServiceRepeat.MonthlyByDate:
                    s.Interval = Math.Clamp(MonthlyInterval, 1, 12);
                    s.DayOfMonth = DayOfMonth;
                    break;
                case ServiceRepeat.Dates:
                    s.StartDate = null;
                    s.EndDate = null;
                    break;
            }

            // Specific dates are only kept for "Specific dates" services; blank rows are ignored.
            s.Dates.Clear();
            if (s.RepeatType == ServiceRepeat.Dates)
            {
                foreach (var d in (Dates ?? new List<ServiceDateForm>()).Where(d => d.Date.HasValue)
                             .GroupBy(d => (d.Date!.Value, Normalise(d.Time))).Select(g => g.First()).OrderBy(d => d.Date))
                {
                    s.Dates.Add(new ServiceDate { ServiceTimesId = s.Id, Date = d.Date!.Value, Time = Normalise(d.Time) ?? (string.IsNullOrWhiteSpace(d.Time) ? null : d.Time.Trim()) });
                }
            }

            s.UseCustomLocation = UseCustomLocation;
            s.LocationChurchId = null;
            s.LocationAddress = null;
            if (UseCustomLocation)
            {
                if (LocationKind == "church") s.LocationChurchId = locationChurchId;
                else s.LocationAddress = string.IsNullOrWhiteSpace(LocationAddress) ? null : LocationAddress.Trim();
            }
        }

        private static string? Normalise(string? time) =>
            ServiceSchedule.ParseTime(time)?.ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    public class ServiceDateForm
    {
        public DateOnly? Date { get; set; }
        public string? Time { get; set; }
    }
}
