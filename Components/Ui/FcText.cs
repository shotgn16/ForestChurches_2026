using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace ForestChurches.Components.Ui
{
    /// <summary>
    /// Small, dependency-free helpers the redesigned views use to show user and ChurchSuite text safely.
    /// </summary>
    public static class FcText
    {
        private static readonly Regex BreakTags = new(@"<\s*(br|/p|/li|/h[1-6]|/div)\s*/?\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex AnyTag = new(@"<[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        private static readonly Regex ExtraBlankLines = new(@"\n{3,}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        /// <summary>
        /// Turns stored text that may contain HTML (e.g. ChurchSuite event descriptions, or values that were
        /// HTML-encoded on the way in) into plain text. Razor then encodes it once when rendering, so nothing
        /// from the source can run as markup and entities like &amp;#x27; display as the character they mean.
        /// </summary>
        public static string PlainText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var text = WebUtility.HtmlDecode(value);            // undo any earlier encoding first
            text = BreakTags.Replace(text, "\n");
            text = AnyTag.Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text);                 // entities that were inside the markup
            text = text.Replace("\r\n", "\n").Replace(' ', ' ');
            text = ExtraBlankLines.Replace(text, "\n\n");
            return text.Trim();
        }

        /// <summary>Formats a stored service time ("9:00", "09:00:00", "18:30") as 24-hour "HH:mm".</summary>
        public static string ServiceTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
                ? t.ToString("HH:mm", CultureInfo.InvariantCulture)
                : value.Trim();
        }

        /// <summary>Sort key for service times so they list in order of the day.</summary>
        public static TimeOnly ServiceTimeSortKey(string? value) =>
            TimeOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : TimeOnly.MaxValue;

        /// <summary>"10:00–13:00" (or just "19:30" when there is no sensible end time).</summary>
        public static string TimeRange(TimeOnly start, TimeOnly end) =>
            end > start
                ? $"{start.ToString("HH:mm", CultureInfo.InvariantCulture)}–{end.ToString("HH:mm", CultureInfo.InvariantCulture)}"
                : start.ToString("HH:mm", CultureInfo.InvariantCulture);

        /// <summary>"Sat · 10:00–13:00" style label for an event.</summary>
        public static string EventWhen(DateOnly date, TimeOnly start, TimeOnly end) =>
            $"{date.ToString("ddd", CultureInfo.CurrentCulture)} · {TimeRange(start, end)}";

        /// <summary>A short excerpt of plain text, cut on a word boundary.</summary>
        public static string Excerpt(string? value, int maxLength = 160)
        {
            var text = PlainText(value).Replace('\n', ' ');
            if (text.Length <= maxLength) return text;
            var cut = text.LastIndexOf(' ', maxLength);
            return text[..(cut > 40 ? cut : maxLength)].TrimEnd(',', '.', ';', ' ') + "…";
        }
    }
}
