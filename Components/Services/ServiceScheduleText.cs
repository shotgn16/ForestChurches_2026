using System.Globalization;
using ForestChurches.Models;
using Microsoft.Extensions.Localization;

namespace ForestChurches.Components.Services
{
    /// <summary>
    /// Describes a service's schedule in words, in the visitor's language, e.g.
    /// "1st and 3rd Sunday of the month, 10:30–11:30" or "Every other Wednesday, 19:00 (from 4 November 2026)".
    /// Wording comes from SharedResources (FC_SVC_* keys); day and month names come from .NET's culture data.
    /// </summary>
    public static class ServiceScheduleText
    {
        public static string Describe(ServiceTimes s, IStringLocalizer L, CultureInfo? culture = null, DateOnly? today = null)
        {
            culture ??= CultureInfo.CurrentUICulture;
            var pattern = Capitalise(Pattern(s, L, culture), culture);
            var time = TimeText(s);
            var text = string.IsNullOrEmpty(time) || s.RepeatType == ServiceRepeat.Dates
                ? pattern
                : string.Format(culture, L["FC_SVC_PATTERN_AT_TIME"].Value, pattern, time);

            var now = today ?? DateOnly.FromDateTime(ServiceSchedule.UkNow());
            var extras = new List<string>();
            if (s.RepeatType != ServiceRepeat.Dates && s.StartDate is { } start && start > now)
                extras.Add(string.Format(culture, L["FC_SVC_FROM"].Value, LongDate(start, culture)));
            if (s.RepeatType != ServiceRepeat.Dates && s.EndDate is { } end)
                extras.Add(string.Format(culture, L["FC_SVC_UNTIL"].Value, LongDate(end, culture)));
            return extras.Count == 0 ? text : $"{text} ({string.Join(", ", extras)})";
        }

        /// <summary>"10:30" or "10:30–11:30".</summary>
        public static string TimeText(ServiceTimes s)
        {
            var start = ServiceSchedule.ParseTime(s.Time);
            if (start is null) return s.Time?.Trim() ?? string.Empty;
            var end = ServiceSchedule.ParseTime(s.EndTime);
            return end is { } e && e > start
                ? $"{start.Value.ToString("HH:mm", CultureInfo.InvariantCulture)}–{e.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                : start.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>"Sunday 1 November, 10:30", the format used for a single upcoming occurrence.</summary>
        public static string Occurrence(ServiceOccurrence o, CultureInfo? culture = null)
        {
            culture ??= CultureInfo.CurrentUICulture;
            var date = o.Date.ToDateTime(TimeOnly.MinValue).ToString("dddd d MMMM", culture);
            return o.Time is { } t ? $"{date}, {t.ToString("HH:mm", CultureInfo.InvariantCulture)}" : date;
        }

        /// <summary>Short form for lists, e.g. "Sun 1 Nov".</summary>
        public static string ShortDate(DateOnly d, CultureInfo? culture = null) =>
            d.ToDateTime(TimeOnly.MinValue).ToString("ddd d MMM", culture ?? CultureInfo.CurrentUICulture);

        public static string LongDate(DateOnly d, CultureInfo? culture = null) =>
            d.ToDateTime(TimeOnly.MinValue).ToString("d MMMM yyyy", culture ?? CultureInfo.CurrentUICulture);

        public static string DayName(int dayOfWeek, CultureInfo? culture = null) =>
            (culture ?? CultureInfo.CurrentUICulture).DateTimeFormat.GetDayName((DayOfWeek)Math.Clamp(dayOfWeek, 0, 6));

        private static string Pattern(ServiceTimes s, IStringLocalizer L, CultureInfo culture)
        {
            var day = DayName(s.DayOfWeek, culture);
            var n = Math.Max(1, s.Interval);
            switch (s.RepeatType)
            {
                case ServiceRepeat.Dates:
                    return L["FC_SVC_DATES"].Value;

                case ServiceRepeat.MonthlyByWeekday:
                {
                    var weeks = ServiceSchedule.ParseWeeks(s.WeeksOfMonth).Select(w => L[w == -1 ? "FC_SVC_ORD_LAST" : $"FC_SVC_ORD_{w}"].Value).ToList();
                    var list = JoinAnd(weeks, L);
                    return n switch
                    {
                        1 => string.Format(culture, L["FC_SVC_MONTHLY_WEEKDAY"].Value, day, list),
                        2 => string.Format(culture, L["FC_SVC_MONTHLY_WEEKDAY_ALT"].Value, day, list),
                        _ => string.Format(culture, L["FC_SVC_MONTHLY_WEEKDAY_N"].Value, day, list, n),
                    };
                }

                case ServiceRepeat.MonthlyByDate:
                {
                    var dom = DayOfMonthText(s.DayOfMonth ?? 1, L, culture);
                    return n switch
                    {
                        1 => string.Format(culture, L["FC_SVC_MONTHLY_DATE"].Value, dom),
                        2 => string.Format(culture, L["FC_SVC_MONTHLY_DATE_ALT"].Value, dom),
                        _ => string.Format(culture, L["FC_SVC_MONTHLY_DATE_N"].Value, dom, n),
                    };
                }

                default:
                    return n switch
                    {
                        1 => string.Format(culture, L["FC_SVC_WEEKLY"].Value, day),
                        2 => string.Format(culture, L["FC_SVC_FORTNIGHTLY"].Value, day),
                        _ => string.Format(culture, L["FC_SVC_EVERY_N_WEEKS"].Value, day, n),
                    };
            }
        }

        /// <summary>"1st", "2nd", "23rd" in English; other languages use the FC_SVC_DAY_NUMBER format ("{0}.", "{0}" …).</summary>
        public static string DayOfMonthText(int day, IStringLocalizer L, CultureInfo culture)
        {
            if (culture.TwoLetterISOLanguageName == "en")
            {
                var suffix = (day % 100) is 11 or 12 or 13 ? "th" : (day % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
                return day.ToString(CultureInfo.InvariantCulture) + suffix;
            }
            return string.Format(culture, L["FC_SVC_DAY_NUMBER"].Value, day);
        }

        private static string Capitalise(string text, CultureInfo culture) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], culture) + text[1..];

        private static string JoinAnd(IReadOnlyList<string> items, IStringLocalizer L) => items.Count switch
        {
            0 => string.Empty,
            1 => items[0],
            _ => $"{string.Join(", ", items.Take(items.Count - 1))} {L["FC_SVC_AND"].Value} {items[^1]}",
        };
    }
}
