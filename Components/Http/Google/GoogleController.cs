using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace ForestChurches.Components.Http.Google
{
    public class GoogleController : Controller, GoogleInterface
    {
        private ILogger<GoogleController> _logger;
        private IHttpWrapper _httpWrapper;
        private Configuration.Configuration _configuration;

        public GoogleController(ILogger<GoogleController> logger, IHttpWrapper httpWrapper, Configuration.Configuration configuration)
        {
            _logger = logger;
            _httpWrapper = httpWrapper;
            _configuration = configuration;
        }

        public async Task<double[]> ConvertToCoordinates(string address)
        {
            double[] coordinates = new double[2];

            if (!string.IsNullOrEmpty(_configuration.Client.GetSecret("google-api-key").Value.Value))
            {
                (string, HttpStatusCode) response = await _httpWrapper.GetAsync(
                    $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(address)}&key={_configuration.Client.GetSecret("google-api-key").Value.Value}");

                if (response.Item2 != HttpStatusCode.OK)
                {
                    _logger.LogError(response.Item2 + "\n" + response.Item1);
                }
                else if (response.Item2 == HttpStatusCode.OK)
                {
                    // Parses the response and returns the 1st Address from the API!
                    GeocodingAPI.Root reverseGeocoding = JsonConvert.DeserializeObject<GeocodingAPI.Root>(response.Item1);

                    if (reverseGeocoding.results != null && reverseGeocoding.results.Count > 0)
                    {
                        coordinates = new double[]
                        {
                                reverseGeocoding.results[0].geometry.location.lat,
                                reverseGeocoding.results[0].geometry.location.lng
                        };
                    }
                    else
                    {
                        _logger.LogError("No results found for the given address.");
                    }
                }
            }

            return coordinates;
        }

        public async Task<List<PlacesAPI.Place>> GetNearbyPlaces(double latitude, double longitute, string fieldType = "more", double radius = 5.0)
        {
            List<PlacesAPI.Place> returnedPlaces = new();

            (string, HttpStatusCode) resonse = await _httpWrapper.PostAsync($"https://places.googleapis.com/v1/places:searchText?key=" +
                $"{_configuration.Client.GetSecret("google-api-key").Value.Value}&fields={_configuration.Client.GetSecret("google-api-fields-more").Value.Value}",
                    new StringContent("{\r\n\"textQuery\":\"Church's nearby\",\r\n\r\n\"locationBias\":{\r\n\"circle\":{\r\n\"center\":{\r\n\"latitude\":" + latitude + ",\r\n\"longitude\":" + longitute + "\r\n },\r\n \"radius\": " + radius + "\r\n}\r\n}\r\n}", Encoding.UTF8, "application/json"));

            if (resonse.Item2 != HttpStatusCode.OK)
            {
                _logger.LogError(resonse.Item2 + "\n" + resonse.Item1);
            }
            else if (resonse.Item2 == HttpStatusCode.OK)
            {
                var json = JsonConvert.DeserializeObject<PlacesAPI.Root>(resonse.Item1);

                foreach (var place in json.places)
                {
                    returnedPlaces.Add(place);
                }
            }

            return returnedPlaces;
        }

        public async Task<Details> GetPlaceInformation(string placeID)
        {
            Details placeDetails = null;

            string requestUri = $"https://maps.googleapis.com/maps/api/place/details/json?place_id={placeID}&key={_configuration.Client.GetSecret("google-api-key").Value.Value}";

            (string, HttpStatusCode) response = await _httpWrapper.GetAsync(requestUri);

            if (response.Item2 == HttpStatusCode.OK)
            {
                placeDetails = JsonConvert.DeserializeObject<Details>(response.Item1);
            }
            else
            {
                _logger.LogError(response.Item2 + "\n" + response.Item1);
            }

            return placeDetails;
        }

        public async Task<string> ConvertToAddress(double latitude, double longitude)
        {
            string address = string.Empty;

            string requestUri = $"https://maps.googleapis.com/maps/api/geocode/json?latlng=" + latitude + "," + longitude + "&key=" + _configuration.Client.GetSecret("google-api-key").Value.Value + "";

            (string, HttpStatusCode) response = await _httpWrapper.GetAsync(requestUri);

            if (response.Item2 == HttpStatusCode.OK)
            {
                GeocodeResponse geocodeResponse = JsonConvert.DeserializeObject<GeocodeResponse>(response.Item1);

                if (geocodeResponse.results != null && geocodeResponse.results.Count > 2)
                {
                    address = geocodeResponse.results[2].formatted_address;
                }
                else
                {
                    _logger.LogError("No results found for the given coordinates.");
                }
            }

            return address;
        }
    }
}
