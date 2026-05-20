namespace ForestChurches.Models
{
    public class AddressComponents
    {
        public string compound_code { get; set; }
        public string global_code { get; set; }

    }
    public class Address_components
    {
        public string long_name { get; set; }
        public string short_name { get; set; }
        public IList<string> types { get; set; }

    }
    public class Northeast1
    {
        public double lat { get; set; }
        public double lng { get; set; }

    }
    public class Southwest1
    {
        public double lat { get; set; }
        public double lng { get; set; }

    }
    public class Bounds
    {
        public Northeast northeast { get; set; }
        public Southwest southwest { get; set; }

    }
    public class Location1
    {
        public double lat { get; set; }
        public double lng { get; set; }

    }
    public class Northeast2
    {
        public double lat { get; set; }
        public double lng { get; set; }

    }
    public class Southwest2
    {
        public double lat { get; set; }
        public double lng { get; set; }

    }
    public class Viewport1
    {
        public Northeast northeast { get; set; }
        public Southwest southwest { get; set; }

    }
    public class Geometry1
    {
        public Bounds bounds { get; set; }
        public Location location { get; set; }
        public string location_type { get; set; }
        public Viewport viewport { get; set; }

    }
    public class Plus_code
    {
        public string compound_code { get; set; }
        public string global_code { get; set; }

    }
    public class Results
    {
        public IList<Address_components> address_components { get; set; }
        public string formatted_address { get; set; }
        public Geometry geometry { get; set; }
        public string place_id { get; set; }
        public Plus_code plus_code { get; set; }
        public IList<string> types { get; set; }

    }
    public class Application
    {
        public Plus_code plus_code { get; set; }
        public IList<Results> results { get; set; }
        public string status { get; set; }

    }
}
