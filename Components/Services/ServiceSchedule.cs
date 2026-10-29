using System.Globalization;
using ForestChurches.Models;

namespace ForestChurches.Components.Services
{
    /// <summary>One date (and time, when known) on which a service takes place.</summary>
    public readonly record struct ServiceOccurrence(DateOnly Date, TimeOnly? Time)
    {
        public DateTime SortKey => Date.ToDateTime(Time ?? TimeOnly.MinValue);
    }

    /// <summary>
    /// Works out when a service takes place from its schedule. Pure functions with no database or
    /// web dependencies, so they're easy to test. All dates are UK local dates.
    /// </summary>
    public static class ServiceSchedule
    {
        /// <summary>How far ahead occurrences are searched, so a schedule can never loop forever.</summary>
        private const int HorizonYears = 5;

        private static readonly Lazy<TimeZoneInfo> UkZone = new(() =>
        {
            foreach (var id in new[] { "Europe/London", "GMT Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { /* try the next id */ }
            }
            return TimeZoneInfo.Utc;
        });

        /// <summary>The current date and time in the UK.</summary>
        public static DateTime UkNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UkZone.Value);

        /// <summary>Parses "HH:mm", "H:mm", "HH:mm:ss" or "9:00 AM"; null when empty or unreadable.</summary>
        public static TimeOnly? ParseTime(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            TimeOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;

        /// <summary>Weeks of the month for MonthlyByWeekday: 1–5, or -1 for the last. Invalid entries are ignored.</summary>
        public static IReadOnlyList<int> ParseWeeks(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Array.Empty<int>();
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0)
                .Where(n => n is >= 1 and <= 5 or -1)
                .Distinct()
                .OrderBy(n => n == -1 ? 6 : n)
                .ToList();
        }

        /// <summary>
        /// Every occurrence on or after <paramref name="from"/>, in date order, until the schedule's end date
        /// or the search horizon. Lazily evaluated: take only what you need.
        /// </summary>
        public static IEnumerable<ServiceOccurrence> Occurrences(ServiceTimes service, DateOnly from)
        {
            ArgumentNullException.ThrowIfNull(service);
            var start = service.StartDate is { } sd && sd > from ? sd : from;
            var last = from.AddYears(HorizonYears);
            if (service.EndDate is { } ed && ed < last) last = ed;
            if (start > last) yield break;

            var time = ParseTime(service.Time);
            var interval = Math.Max(1, service.Interval);

            switch (service.RepeatType)
            {
                case ServiceRepeat.Dates:
                    foreach (var d in (service.Dates ?? new List<ServiceDate>())
                                 .Select(d => new ServiceOccurrence(d.Date, ParseTime(d.Time) ?? time))
                                 .Where(o => o.Date >= start && o.Date <= last)
                                 .OrderBy(o => o.SortKey))
                    {
                        yield return d;
                    }
                    yield break;

                case ServiceRepeat.MonthlyByWeekday:
                {
                    var weeks = ParseWeeks(service.WeeksOfMonth);
                    if (weeks.Count == 0) yield break;
                    var dow = (DayOfWeek)Math.Clamp(service.DayOfWeek, 0, 6);
                    foreach (var month in Months(service, start, last, interval))
                    {
                        var dates = weeks.Select(w => NthWeekdayOfMonth(month.Year, month.Month, dow, w))
                            .Where(d => d.HasValue).Select(d => d!.Value).Distinct().OrderBy(d => d);
                        foreach (var d in dates)
                        {
                            if (d >= start && d <= last) yield return new ServiceOccurrence(d, time);
                        }
                    }
                    yield break;
                }

                case ServiceRepeat.MonthlyByDate:
                {
                    if (service.DayOfMonth is not { } dom) yield break;
                    foreach (var month in Months(service, start, last, interval))
                    {
                        var day = Math.Min(Math.Clamp(dom, 1, 31), DateTime.DaysInMonth(month.Year, month.Month));
                        var d = new DateOnly(month.Year, month.Month, day);
                        if (d >= start && d <= last) yield return new ServiceOccurrence(d, time);
                    }
                    yield break;
                }

                default: // Weekly (and anything unrecognised, which is how pre-schedule rows behave)
                {
                    var dow = (DayOfWeek)Math.Clamp(service.DayOfWeek, 0, 6);
                    var d = NextOnOrAfter(start, dow);
                    if (interval > 1)
                    {
                        // The pattern's weeks are counted from the first matching day on/after StartDate.
                        var anchor = NextOnOrAfter(service.StartDate ?? start, dow);
                        var weeksFromAnchor = (d.DayNumber - anchor.DayNumber) / 7;
                        var offset = ((weeksFromAnchor % interval) + interval) % interval;
                        if (offset != 0) d = d.AddDays(7 * (interval - offset));
                    }
                    for (; d <= last; d = d.AddDays(7 * interval))
                    {
                        yield return new ServiceOccurrence(d, time);
                    }
                    yield break;
                }
            }
        }

        /// <summary>
        /// The next <paramref name="count"/> occurrences that haven't started yet, relative to <paramref name="now"/>
        /// (UK local time). Occurrences earlier today drop off once their time has passed.
        /// </summary>
        public static IReadOnlyList<ServiceOccurrence> Upcoming(ServiceTimes service, DateTime now, int count)
        {
            var today = DateOnly.FromDateTime(now);
            var nowTime = TimeOnly.FromDateTime(now);
            return Occurrences(service, today)
                .Where(o => o.Date > today || o.Time is null || o.Time.Value >= nowTime)
                .Take(Math.Max(0, count))
                .ToList();
        }

        /// <summary>The next occurrence across several services, with the service it belongs to.</summary>
        public static (ServiceTimes Service, ServiceOccurrence Occurrence)? NextAcross(IEnumerable<ServiceTimes> services, DateTime now)
        {
            (ServiceTimes, ServiceOccurrence)? best = null;
            foreach (var s in services ?? Enumerable.Empty<ServiceTimes>())
            {
                var next = Upcoming(s, now, 1);
                if (next.Count == 0) continue;
                if (best is null || next[0].SortKey < best.Value.Item2.SortKey) best = (s, next[0]);
            }
            return best;
        }

        /// <summary>
        /// Checks a schedule and returns problems as resource keys (empty when it's valid). Callers show
        /// these through the localizer.
        /// </summary>
        public static IReadOnlyList<string> Validate(ServiceTimes s)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(s.Note)) errors.Add("FC_SVC_ERR_NAME");
            if ((s.Note ?? string.Empty).Length > 200) errors.Add("FC_SVC_ERR_NAME_LONG");
            if (ParseTime(s.Time) is null) errors.Add("FC_SVC_ERR_TIME");
            if (!string.IsNullOrWhiteSpace(s.EndTime))
            {
                var end = ParseTime(s.EndTime);
                if (end is null || (ParseTime(s.Time) is { } st && end <= st)) errors.Add("FC_SVC_ERR_END_TIME");
            }
            if (!ServiceRepeat.All.Contains(s.RepeatType)) errors.Add("FC_SVC_ERR_REPEAT");
            if (s.Interval is < 1 or > 52) errors.Add("FC_SVC_ERR_INTERVAL");
            if (s.RepeatType != ServiceRepeat.Dates && s.Interval > 1 && s.StartDate is null) errors.Add("FC_SVC_ERR_START_NEEDED");
            if (s.RepeatType == ServiceRepeat.MonthlyByWeekday && ParseWeeks(s.WeeksOfMonth).Count == 0) errors.Add("FC_SVC_ERR_WEEKS");
            if (s.RepeatType == ServiceRepeat.MonthlyByDate && s.DayOfMonth is not (>= 1 and <= 31)) errors.Add("FC_SVC_ERR_DAY_OF_MONTH");
            if (s.RepeatType == ServiceRepeat.Dates && (s.Dates == null || s.Dates.Count == 0)) errors.Add("FC_SVC_ERR_DATES");
            if (s.RepeatType == ServiceRepeat.Dates && s.Dates != null && s.Dates.Any(d => !string.IsNullOrWhiteSpace(d.Time) && ParseTime(d.Time) is null)) errors.Add("FC_SVC_ERR_TIME");
            if (s.StartDate is { } a && s.EndDate is { } b && b < a) errors.Add("FC_SVC_ERR_END_DATE");
            if (s.UseCustomLocation && string.IsNullOrWhiteSpace(s.LocationChurchId) && string.IsNullOrWhiteSpace(s.LocationAddress)) errors.Add("FC_SVC_ERR_LOCATION");
            return errors;
        }

        // ---- helpers ------------------------------------------------------------------------

        private static DateOnly NextOnOrAfter(DateOnly date, DayOfWeek dow) =>
            date.AddDays(((int)dow - (int)date.DayOfWeek + 7) % 7);

        /// <summary>The nth (1–5) or last (-1) given weekday of a month, or null if that month has no such day.</summary>
        public static DateOnly? NthWeekdayOfMonth(int year, int month, DayOfWeek dow, int n)
        {
            if (n == -1)
            {
                var lastDay = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
                return lastDay.AddDays(-(((int)lastDay.DayOfWeek - (int)dow + 7) % 7));
            }
            var first = NextOnOrAfter(new DateOnly(year, month, 1), dow);
            var d = first.AddDays(7 * (n - 1));
            return d.Month == month ? d : null;
        }

        /// <summary>The months (as their 1st day) the pattern runs in, from start's month to last's month.</summary>
        private static IEnumerable<DateOnly> Months(ServiceTimes s, DateOnly start, DateOnly last, int interval)
        {
            var anchor = s.StartDate ?? start;
            var anchorIndex = anchor.Year * 12 + anchor.Month - 1;
            var m = new DateOnly(start.Year, start.Month, 1);
            var lastMonth = new DateOnly(last.Year, last.Month, 1);
            for (; m <= lastMonth; m = m.AddMonths(1))
            {
                var index = m.Year * 12 + m.Month - 1;
                if (((index - anchorIndex) % interval + interval) % interval == 0) yield return m;
            }
        }
    }
}
