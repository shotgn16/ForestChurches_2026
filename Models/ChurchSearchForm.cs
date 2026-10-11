namespace ForestChurches.Models
{
    /// <summary>
    /// Values for the shared church search form (Pages/Shared/_ChurchSearch.cshtml), used on the
    /// home page hero and above the results on /NearMe. Field names match NearMeModel's bound properties.
    /// </summary>
    public class ChurchSearchForm
    {
        public string? Postcode { get; set; }
        public double? MaxDistanceMiles { get; set; } = 5.0;
        public string? SelectedDenomination { get; set; }
        public IReadOnlyList<string> Denominations { get; set; } = Array.Empty<string>();

        /// <summary>Bound property name (e.g. "FilterParking") → whether it is ticked.</summary>
        public IReadOnlyDictionary<string, bool> Filters { get; set; } = new Dictionary<string, bool>();

        /// <summary>True when shown inside the photo hero (adds the shadowed card look).</summary>
        public bool InHero { get; set; }

        /// <summary>Open the "More filters" panel on first render.</summary>
        public bool FiltersOpen { get; set; }

        /// <summary>Facility filters in display order: bound property name and its localisation key.</summary>
        public static readonly (string Name, string LabelKey)[] Facilities =
        {
            ("FilterWheelchairAccess", "NEARME_FACILITY_WHEELCHAIR_ACCESS"),
            ("FilterParking", "NEARME_FACILITY_PARKING"),
            ("FilterHearingAidSupport", "NEARME_FACILITY_HEARING_AID_SUPPORT"),
            ("FilterRefreshments", "NEARME_FACILITY_REFRESHMENTS"),
            ("FilterChildcare", "NEARME_FACILITY_CHILDRENS_MINISTRY"),
            ("FilterRestrooms", "NEARME_FACILITY_RESTROOMS"),
            ("FilterWifi", "NEARME_FACILITY_WIFI"),
            ("FilterKitchen", "NEARME_FACILITY_KITCHEN"),
            ("FilterMeetingHall", "NEARME_FACILITY_COMMUNITY_HALL"),
            ("FilterWorshipHall", "NEARME_FACILITY_WORSHIP_HALL"),
            ("FilterOutdoorSpaces", "NEARME_FACILITY_OUTDOOR_SPACE"),
        };
    }
}
