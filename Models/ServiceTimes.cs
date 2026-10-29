using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Models
{
    /// <summary>How a service repeats. Stored as text so the database stays readable.</summary>
    public static class ServiceRepeat
    {
        /// <summary>On one weekday, every <see cref="ServiceTimes.Interval"/> weeks (1 = weekly, 2 = fortnightly…).</summary>
        public const string Weekly = "Weekly";

        /// <summary>On given weeks of the month (1st, 3rd, last…) on one weekday, every N months.</summary>
        public const string MonthlyByWeekday = "MonthlyByWeekday";

        /// <summary>On a date of the month (e.g. the 15th), every N months.</summary>
        public const string MonthlyByDate = "MonthlyByDate";

        /// <summary>Only on the specific dates (and times) listed in <see cref="ServiceTimes.Dates"/>.</summary>
        public const string Dates = "Dates";

        public static readonly string[] All = { Weekly, MonthlyByWeekday, MonthlyByDate, Dates };
    }

    /// <summary>
    /// A regular service at a church. Older rows (before schedules existed) have the defaults
    /// below, which mean "every Sunday at <see cref="Time"/>", exactly what they meant before.
    /// </summary>
    public class ServiceTimes
    {
        [ValidateNever]
        [Column("ServiceID")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>Start time, "HH:mm". For <see cref="ServiceRepeat.Dates"/> it's the default for dates without their own time.</summary>
        [Required]
        public string Time { get; set; }

        /// <summary>Optional finish time, "HH:mm".</summary>
        [StringLength(5)]
        public string? EndTime { get; set; }

        /// <summary>The service's name or description, e.g. "Holy Communion".</summary>
        [Required]
        public string Note { get; set; }

        // ---- Schedule -------------------------------------------------------------------

        [Required]
        [StringLength(20)]
        public string RepeatType { get; set; } = ServiceRepeat.Weekly;

        /// <summary>Every N weeks (Weekly) or every N months (monthly types). 1 = every week/month.</summary>
        [Range(1, 52)]
        public int Interval { get; set; } = 1;

        /// <summary>Day of the week for Weekly and MonthlyByWeekday (0 = Sunday … 6 = Saturday).</summary>
        [Range(0, 6)]
        public int DayOfWeek { get; set; } = 0;

        /// <summary>For MonthlyByWeekday: comma-separated weeks of the month, 1–5, or -1 for the last (e.g. "1,3" or "-1").</summary>
        [StringLength(20)]
        public string? WeeksOfMonth { get; set; }

        /// <summary>For MonthlyByDate: day of the month, 1–31 (in shorter months, the last day).</summary>
        [Range(1, 31)]
        public int? DayOfMonth { get; set; }

        /// <summary>
        /// First date the pattern applies from. Required when <see cref="Interval"/> is more than 1, because it
        /// fixes *which* fortnight or month the service falls in.
        /// </summary>
        public DateOnly? StartDate { get; set; }

        /// <summary>Optional last date (inclusive).</summary>
        public DateOnly? EndDate { get; set; }

        /// <summary>Specific dates for <see cref="ServiceRepeat.Dates"/>.</summary>
        [ValidateNever]
        public ICollection<ServiceDate> Dates { get; set; } = new List<ServiceDate>();

        // ---- Location -------------------------------------------------------------------

        /// <summary>
        /// Off: the service is held at the church it's listed under. On: it's held at
        /// <see cref="LocationChurchId"/> (another church on the same account) or, if that's empty, at <see cref="LocationAddress"/>.
        /// </summary>
        public bool UseCustomLocation { get; set; }

        /// <summary>Another church (managed by the same account) where this service is held.</summary>
        [ValidateNever]
        public string? LocationChurchId { get; set; }

        [ValidateNever]
        [ForeignKey(nameof(LocationChurchId))]
        public ChurchInformation? LocationChurch { get; set; }

        /// <summary>A typed address, e.g. a village hall or a school.</summary>
        [StringLength(300)]
        public string? LocationAddress { get; set; }

        // ---- Owner ----------------------------------------------------------------------

        [ValidateNever]
        [ForeignKey("ChurchInformationId")]
        public string ChurchInformationId { get; set; }

        [ValidateNever]
        public ChurchInformation ChurchInformation { get; set; }
    }

    /// <summary>One specific date (and optionally its own time) of a <see cref="ServiceRepeat.Dates"/> service.</summary>
    public class ServiceDate
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ValidateNever]
        public string ServiceTimesId { get; set; }

        [ValidateNever]
        public ServiceTimes Service { get; set; }

        public DateOnly Date { get; set; }

        /// <summary>"HH:mm"; when empty the service's own <see cref="ServiceTimes.Time"/> is used.</summary>
        [StringLength(5)]
        public string? Time { get; set; }
    }
}
