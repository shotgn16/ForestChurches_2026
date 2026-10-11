using System.Text.Json.Serialization;

namespace ForestChurches.Models
{
    public class ChurchSuiteProfileResponse
    {
        [JsonPropertyName("data")]
        public ChurchSuiteAccountDetails? Data { get; set; }

        [JsonPropertyName("account")]
        public ChurchSuiteAccountDetails? AccountLegacy { get; set; }

        [JsonIgnore]
        public ChurchSuiteAccountDetails? Account => Data ?? AccountLegacy;
    }

    public class ChurchSuiteAddress
    {
        [JsonPropertyName("line_1")] public string? Line1 { get; set; }
        [JsonPropertyName("line_2")] public string? Line2 { get; set; }
        [JsonPropertyName("line_3")] public string? Line3 { get; set; }
        [JsonPropertyName("city")] public string? City { get; set; }
        [JsonPropertyName("county")] public string? County { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
        [JsonPropertyName("postcode")] public string? Postcode { get; set; }
        [JsonPropertyName("latitude")] public double Latitude { get; set; }
        [JsonPropertyName("longitude")] public double Longitude { get; set; }
    }

    public class Contact
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("telephone")] public string? Telephone { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
    }
}