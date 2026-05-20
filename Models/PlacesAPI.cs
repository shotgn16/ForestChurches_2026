namespace ForestChurches.Models
{
    public class PlacesAPI
    {
        public override string ToString()
        {
            return base.ToString();
        }
        public class AccessibilityOptions
        {
            public bool wheelchairAccessibleParking { get; set; }
            public bool wheelchairAccessibleEntrance { get; set; }
        }

        public class DisplayName
        {
            public string text { get; set; }
        }

        public class Place
        {
            public string id { get; set; }

            public string formattedAddress { get; set; }
            public DisplayName displayName { get; set; }
            public string nationalPhoneNumber { get; set; }
            public string websiteUri { get; set; }
            public AccessibilityOptions accessibilityOptions { get; set; }
            public RegularOpeningHours regularOpeningHours { get; set; }
        }

        public class RegularOpeningHours
        {
            public List<string> weekdayDescriptions { get; set; }

        }
        public class Root
        {
            public List<Place> places { get; set; }
        }
    }
}
