namespace ForestChurches.Models
{
    public class GeocodeResponse
    {
        public IList<Results1> results { get; set; }
        public string status { get; set; }
    }

    public class Results1
    {
        public IList<Address_components> address_components { get; set; }
        public string formatted_address { get; set; }
        // Other properties can be added as needed
    }
}
