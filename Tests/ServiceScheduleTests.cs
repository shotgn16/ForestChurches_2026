using ForestChurches.Components.Services;
using ForestChurches.Models;
using Xunit;

namespace ForestChurches.Tests
{
    public class ServiceScheduleTests
    {
        private static ServiceTimes Svc(string repeat = ServiceRepeat.Weekly, int dow = 0, int interval = 1,
            string? weeks = null, int? dom = null, DateOnly? start = null, DateOnly? end = null, string time = "10:30") =>
            new() { RepeatType = repeat, DayOfWeek = dow, Interval = interval, WeeksOfMonth = weeks, DayOfMonth = dom, StartDate = start, EndDate = end, Time = time, Note = "Test" };

        private static DateOnly D(int y, int m, int d) => new(y, m, d);

        private static List<DateOnly> First(ServiceTimes s, DateOnly from, int n) =>
            ServiceSchedule.Occurrences(s, from).Take(n).Select(o => o.Date).ToList();

        [Fact]
        public void Default_row_is_every_sunday()
        {
            // Rows created before schedules existed only have Time and Note; defaults must mean "every Sunday".
            var s = new ServiceTimes { Time = "09:00", Note = "Old" };
            // 28 Oct 2026 is a Wednesday
            Assert.Equal(new[] { D(2026, 11, 1), D(2026, 11, 8), D(2026, 11, 15) }, First(s, D(2026, 10, 28), 3));
        }

        [Fact]
        public void Weekly_includes_the_start_day_itself()
        {
            Assert.Equal(D(2026, 11, 1), First(Svc(), D(2026, 11, 1), 1)[0]);
        }

        [Fact]
        public void Fortnightly_follows_the_start_date()
        {
            var s = Svc(interval: 2, start: D(2026, 11, 8)); // first service Sun 8 Nov
            Assert.Equal(new[] { D(2026, 11, 8), D(2026, 11, 22), D(2026, 12, 6) }, First(s, D(2026, 10, 28), 3));
            // Asking from part-way through still keeps the same fortnights
            Assert.Equal(new[] { D(2026, 11, 22), D(2026, 12, 6) }, First(s, D(2026, 11, 9), 2));
            Assert.Equal(new[] { D(2026, 11, 22) }, First(s, D(2026, 11, 15), 1));
        }

        [Fact]
        public void Every_three_weeks_on_a_wednesday()
        {
            var s = Svc(dow: 3, interval: 3, start: D(2026, 10, 28)); // Wed
            Assert.Equal(new[] { D(2026, 10, 28), D(2026, 11, 18), D(2026, 12, 9) }, First(s, D(2026, 10, 1), 3));
        }

        [Fact]
        public void First_and_third_sunday_of_the_month()
        {
            var s = Svc(ServiceRepeat.MonthlyByWeekday, weeks: "1,3");
            // Nov 2026: Sundays 1, 8, 15, 22, 29. Dec 2026: 6, 13, 20, 27
            Assert.Equal(new[] { D(2026, 11, 1), D(2026, 11, 15), D(2026, 12, 6), D(2026, 12, 20) }, First(s, D(2026, 10, 28), 4));
        }

        [Fact]
        public void Last_sunday_of_the_month()
        {
            var s = Svc(ServiceRepeat.MonthlyByWeekday, weeks: "-1");
            Assert.Equal(new[] { D(2026, 11, 29), D(2026, 12, 27), D(2027, 1, 31) }, First(s, D(2026, 10, 28), 3));
        }

        [Fact]
        public void Fifth_sunday_only_in_months_that_have_one()
        {
            var s = Svc(ServiceRepeat.MonthlyByWeekday, weeks: "5");
            // Nov 2026 has five Sundays (29th); Dec 2026 doesn't; Jan 2027 does (31st)
            Assert.Equal(new[] { D(2026, 11, 29), D(2027, 1, 31) }, First(s, D(2026, 11, 1), 2));
        }

        [Fact]
        public void Second_sunday_every_other_month_from_start_month()
        {
            var s = Svc(ServiceRepeat.MonthlyByWeekday, weeks: "2", interval: 2, start: D(2026, 11, 1));
            // Nov 2026 2nd Sun = 8th; Jan 2027 2nd Sun = 10th; Mar 2027 2nd Sun = 14th
            Assert.Equal(new[] { D(2026, 11, 8), D(2027, 1, 10), D(2027, 3, 14) }, First(s, D(2026, 10, 1), 3));
        }

        [Fact]
        public void Monthly_by_date_clamps_to_short_months()
        {
            var s = Svc(ServiceRepeat.MonthlyByDate, dom: 31);
            Assert.Equal(new[] { D(2027, 1, 31), D(2027, 2, 28), D(2027, 3, 31), D(2027, 4, 30) }, First(s, D(2027, 1, 1), 4));
        }

        [Fact]
        public void Monthly_by_date_every_two_months()
        {
            var s = Svc(ServiceRepeat.MonthlyByDate, dom: 15, interval: 2, start: D(2026, 12, 1));
            Assert.Equal(new[] { D(2026, 12, 15), D(2027, 2, 15), D(2027, 4, 15) }, First(s, D(2026, 10, 1), 3));
        }

        [Fact]
        public void Custom_dates_are_sorted_and_use_their_own_times()
        {
            var s = Svc(ServiceRepeat.Dates);
            s.Dates.Add(new ServiceDate { Date = D(2026, 12, 24), Time = "23:30" });
            s.Dates.Add(new ServiceDate { Date = D(2026, 12, 25) });
            s.Dates.Add(new ServiceDate { Date = D(2026, 10, 1) }); // past
            var occ = ServiceSchedule.Occurrences(s, D(2026, 10, 28)).ToList();
            Assert.Equal(2, occ.Count);
            Assert.Equal(new TimeOnly(23, 30), occ[0].Time);
            Assert.Equal(new TimeOnly(10, 30), occ[1].Time); // falls back to the service's time
        }

        [Fact]
        public void End_date_stops_the_schedule()
        {
            var s = Svc(end: D(2026, 11, 15));
            Assert.Equal(new[] { D(2026, 11, 1), D(2026, 11, 8), D(2026, 11, 15) }, ServiceSchedule.Occurrences(s, D(2026, 10, 28)).Select(o => o.Date).ToList());
        }

        [Fact]
        public void Future_start_date_is_respected()
        {
            var s = Svc(start: D(2027, 1, 1));
            Assert.Equal(D(2027, 1, 3), First(s, D(2026, 10, 28), 1)[0]);
        }

        [Fact]
        public void Upcoming_skips_a_service_that_already_started_today()
        {
            var s = Svc(time: "10:30");
            var sundayMorning = new DateTime(2026, 11, 1, 9, 0, 0);
            var sundayAfternoon = new DateTime(2026, 11, 1, 13, 0, 0);
            Assert.Equal(D(2026, 11, 1), ServiceSchedule.Upcoming(s, sundayMorning, 1)[0].Date);
            Assert.Equal(D(2026, 11, 8), ServiceSchedule.Upcoming(s, sundayAfternoon, 1)[0].Date);
        }

        [Fact]
        public void Next_across_picks_the_soonest_service()
        {
            var sunday = Svc(time: "10:30");
            var wednesday = Svc(dow: 3, time: "09:00");
            var next = ServiceSchedule.NextAcross(new[] { sunday, wednesday }, new DateTime(2026, 10, 27, 12, 0, 0)); // Tue
            Assert.NotNull(next);
            Assert.Same(wednesday, next!.Value.Service);
            Assert.Equal(D(2026, 10, 28), next.Value.Occurrence.Date);
        }

        [Fact]
        public void Old_style_times_still_parse()
        {
            Assert.Equal(new TimeOnly(9, 0), ServiceSchedule.ParseTime("9:00 AM"));
            Assert.Equal(new TimeOnly(18, 30), ServiceSchedule.ParseTime("18:30:00"));
            Assert.Null(ServiceSchedule.ParseTime("soon"));
        }

        [Fact]
        public void Validation_catches_incomplete_schedules()
        {
            Assert.Empty(ServiceSchedule.Validate(Svc()));
            Assert.Contains("FC_SVC_ERR_START_NEEDED", ServiceSchedule.Validate(Svc(interval: 2)));
            Assert.Contains("FC_SVC_ERR_WEEKS", ServiceSchedule.Validate(Svc(ServiceRepeat.MonthlyByWeekday)));
            Assert.Contains("FC_SVC_ERR_DAY_OF_MONTH", ServiceSchedule.Validate(Svc(ServiceRepeat.MonthlyByDate)));
            Assert.Contains("FC_SVC_ERR_DATES", ServiceSchedule.Validate(Svc(ServiceRepeat.Dates)));
            Assert.Contains("FC_SVC_ERR_END_TIME", ServiceSchedule.Validate(new ServiceTimes { Time = "10:30", EndTime = "10:00", Note = "x" }));
            Assert.Contains("FC_SVC_ERR_END_DATE", ServiceSchedule.Validate(Svc(start: D(2027, 1, 1), end: D(2026, 1, 1))));
            Assert.Contains("FC_SVC_ERR_LOCATION", ServiceSchedule.Validate(new ServiceTimes { Time = "10:30", Note = "x", UseCustomLocation = true }));
            Assert.Contains("FC_SVC_ERR_TIME", ServiceSchedule.Validate(Svc(time: "")));
            Assert.Contains("FC_SVC_ERR_NAME", ServiceSchedule.Validate(new ServiceTimes { Time = "10:30", Note = " " }));
        }

        [Fact]
        public void Invalid_weeks_are_ignored()
        {
            Assert.Equal<int>(new[] { 1, 3, -1 }, ServiceSchedule.ParseWeeks("3, 1, 9, x, -1, 1"));
        }
    }
}
