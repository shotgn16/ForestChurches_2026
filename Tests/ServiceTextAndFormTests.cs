using System.Globalization;
using System.Xml.Linq;
using ForestChurches.Components.Services;
using ForestChurches.Models;
using Microsoft.Extensions.Localization;
using Xunit;

namespace ForestChurches.Tests
{
    /// <summary>Reads the real English SharedResources.resx so missing keys fail the tests.</summary>
    internal sealed class ResxLocalizer : IStringLocalizer
    {
        private static readonly Lazy<Dictionary<string, string>> Strings = new(() =>
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "SharedResources.resx"))) dir = Path.GetDirectoryName(dir);
            if (dir == null) throw new FileNotFoundException("SharedResources.resx not found above " + AppContext.BaseDirectory);
            return XDocument.Load(Path.Combine(dir, "SharedResources.resx")).Root!
                .Elements("data").ToDictionary(d => (string)d.Attribute("name")!, d => (string)d.Element("value")!);
        });

        public LocalizedString this[string name] =>
            Strings.Value.TryGetValue(name, out var v) ? new LocalizedString(name, v) : throw new KeyNotFoundException("Missing resource key " + name);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(CultureInfo.InvariantCulture, this[name].Value, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            Strings.Value.Select(kv => new LocalizedString(kv.Key, kv.Value));
    }

    public class ServiceScheduleTextTests
    {
        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");
        private static readonly IStringLocalizer L = new ResxLocalizer();
        private static readonly DateOnly Today = new(2026, 10, 28);

        private static string Describe(ServiceTimes s) => ServiceScheduleText.Describe(s, L, En, Today);

        [Fact]
        public void Weekly() =>
            Assert.Equal("Every Sunday, 10:30", Describe(new ServiceTimes { Time = "10:30", Note = "x" }));

        [Fact]
        public void Fortnightly_with_end_time_and_future_start() =>
            Assert.Equal("Every other Wednesday, 19:00–20:00 (from 4 November 2026)",
                Describe(new ServiceTimes { Time = "19:00", EndTime = "20:00", Note = "x", DayOfWeek = 3, Interval = 2, StartDate = new DateOnly(2026, 11, 4) }));

        [Fact]
        public void Every_three_weeks() =>
            Assert.Equal("Every 3 weeks on Friday, 09:00",
                Describe(new ServiceTimes { Time = "09:00", Note = "x", DayOfWeek = 5, Interval = 3, StartDate = new DateOnly(2026, 1, 2) }));

        [Fact]
        public void First_and_third_sunday() =>
            Assert.Equal("1st and 3rd Sunday of the month, 10:30",
                Describe(new ServiceTimes { Time = "10:30", Note = "x", RepeatType = ServiceRepeat.MonthlyByWeekday, WeeksOfMonth = "3,1" }));

        [Fact]
        public void Last_sunday_every_other_month_until() =>
            Assert.Equal("Last Sunday of every other month, 18:00 (until 1 March 2027)",
                Describe(new ServiceTimes { Time = "18:00", Note = "x", RepeatType = ServiceRepeat.MonthlyByWeekday, WeeksOfMonth = "-1", Interval = 2, StartDate = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2027, 3, 1) }));

        [Fact]
        public void Three_weeks_listed_with_commas() =>
            Assert.Equal("1st, 2nd and last Sunday of the month, 08:00",
                Describe(new ServiceTimes { Time = "08:00", Note = "x", RepeatType = ServiceRepeat.MonthlyByWeekday, WeeksOfMonth = "1,2,-1" }));

        [Fact]
        public void Monthly_by_date() =>
            Assert.Equal("The 22nd of every month, 12:00",
                Describe(new ServiceTimes { Time = "12:00", Note = "x", RepeatType = ServiceRepeat.MonthlyByDate, DayOfMonth = 22 }));

        [Fact]
        public void Monthly_by_date_every_four_months() =>
            Assert.Equal("The 11th of every 4 months, 12:00",
                Describe(new ServiceTimes { Time = "12:00", Note = "x", RepeatType = ServiceRepeat.MonthlyByDate, DayOfMonth = 11, Interval = 4, StartDate = new DateOnly(2026, 1, 1) }));

        [Fact]
        public void Specific_dates() =>
            Assert.Equal("On selected dates", Describe(new ServiceTimes { Time = "12:00", Note = "x", RepeatType = ServiceRepeat.Dates }));

        [Fact]
        public void Occurrence_formats_day_date_and_time() =>
            Assert.Equal("Sunday 1 November, 10:30", ServiceScheduleText.Occurrence(new ServiceOccurrence(new DateOnly(2026, 11, 1), new TimeOnly(10, 30)), En));

        [Fact]
        public void Every_error_key_exists()
        {
            foreach (var key in new[] { "FC_SVC_ERR_NAME", "FC_SVC_ERR_NAME_LONG", "FC_SVC_ERR_TIME", "FC_SVC_ERR_END_TIME", "FC_SVC_ERR_REPEAT", "FC_SVC_ERR_INTERVAL", "FC_SVC_ERR_START_NEEDED", "FC_SVC_ERR_WEEKS", "FC_SVC_ERR_DAY_OF_MONTH", "FC_SVC_ERR_DATES", "FC_SVC_ERR_END_DATE", "FC_SVC_ERR_LOCATION" })
            {
                Assert.True(!string.IsNullOrWhiteSpace(L[key].Value), key);
            }
        }
    }

    public class ServiceFormTests
    {
        private static ServiceTimes Saved(ServiceForm f, string? locationChurch = null)
        {
            var s = new ServiceTimes { Id = "svc-1", ChurchInformationId = "church-1" };
            f.ApplyTo(s, locationChurch);
            return s;
        }

        [Fact]
        public void Weekly_form_clears_monthly_fields()
        {
            var s = Saved(new ServiceForm { Name = " Evensong ", Time = "18:30", RepeatType = ServiceRepeat.Weekly, WeeklyInterval = 2, MonthlyInterval = 5, DayOfWeek = 0, WeeksOfMonth = new() { 1 }, DayOfMonth = 9, StartDate = new DateOnly(2026, 11, 1) });
            Assert.Equal("Evensong", s.Note);
            Assert.Equal(2, s.Interval);
            Assert.Null(s.WeeksOfMonth);
            Assert.Null(s.DayOfMonth);
            Assert.Empty(ServiceSchedule.Validate(s));
        }

        [Fact]
        public void Monthly_weekday_form_uses_monthly_interval_and_weeks()
        {
            var s = Saved(new ServiceForm { Name = "Family", Time = "10:00", RepeatType = ServiceRepeat.MonthlyByWeekday, WeeklyInterval = 3, MonthlyInterval = 1, WeeksOfMonth = new() { 2, 4, 9 } });
            Assert.Equal(1, s.Interval);
            Assert.Equal("2,4", s.WeeksOfMonth);
        }

        [Fact]
        public void Specific_dates_skip_blank_rows_and_duplicates()
        {
            var s = Saved(new ServiceForm
            {
                Name = "Carols", Time = "18:00", RepeatType = ServiceRepeat.Dates, StartDate = new DateOnly(2026, 1, 1),
                Dates = new() { new() { Date = new DateOnly(2026, 12, 24), Time = "23:30" }, new(), new() { Date = new DateOnly(2026, 12, 20) }, new() { Date = new DateOnly(2026, 12, 24), Time = "23:30" } },
            });
            Assert.Equal(2, s.Dates.Count);
            Assert.Null(s.StartDate); // not used for specific dates
            Assert.Equal(new DateOnly(2026, 12, 20), s.Dates.First().Date);
            Assert.Equal("svc-1", s.Dates.First().ServiceTimesId);
        }

        [Fact]
        public void Location_off_clears_location()
        {
            var s = Saved(new ServiceForm { Name = "x", Time = "10:00", UseCustomLocation = false, LocationKind = "address", LocationAddress = "Village hall" }, "church-2");
            Assert.False(s.UseCustomLocation);
            Assert.Null(s.LocationAddress);
            Assert.Null(s.LocationChurchId);
        }

        [Fact]
        public void Location_church_uses_the_checked_id_only()
        {
            var s = Saved(new ServiceForm { Name = "x", Time = "10:00", UseCustomLocation = true, LocationKind = "church", LocationChurchId = "encrypted-from-browser", LocationAddress = "ignored" }, "church-2");
            Assert.Equal("church-2", s.LocationChurchId);
            Assert.Null(s.LocationAddress);
        }

        [Fact]
        public void Location_address()
        {
            var s = Saved(new ServiceForm { Name = "x", Time = "10:00", UseCustomLocation = true, LocationKind = "address", LocationAddress = "  Village Hall, High St  " });
            Assert.Equal("Village Hall, High St", s.LocationAddress);
            Assert.Empty(ServiceSchedule.Validate(s));
        }

        [Fact]
        public void Round_trip_through_the_form()
        {
            var original = new ServiceTimes
            {
                Id = "svc-9", Note = "Holy Communion", Time = "9:00 AM", EndTime = "10:00", RepeatType = ServiceRepeat.MonthlyByDate,
                DayOfMonth = 15, Interval = 2, StartDate = new DateOnly(2026, 12, 1), UseCustomLocation = true, LocationChurchId = "church-2",
            };
            var form = ServiceForm.From(original, id => "enc:" + id, id => "enc:" + id);
            Assert.Equal("09:00", form.Time);
            Assert.Equal(2, form.MonthlyInterval);
            Assert.Equal("church", form.LocationKind);
            Assert.Equal("enc:church-2", form.LocationChurchId);
            var copy = new ServiceTimes { Id = "svc-9" };
            form.ApplyTo(copy, "church-2");
            Assert.Equal(original.DayOfMonth, copy.DayOfMonth);
            Assert.Equal(original.Interval, copy.Interval);
            Assert.Equal(original.StartDate, copy.StartDate);
            Assert.Equal("09:00", copy.Time);
            Assert.Equal("church-2", copy.LocationChurchId);
        }
    }
}
