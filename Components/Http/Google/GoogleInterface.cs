using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;

namespace ForestChurches.Components.Http.Google
{
    /// <summary>
    ///  This Interface/Controller combo will be used for defining the methods used to communicate with the Google Cloud Console API's and retrieve data for the methods as required...
    /// </summary>
    public interface GoogleInterface
    {
        Task<double[]> ConvertToCoordinates(string address);
        Task<List<PlacesAPI.Place>> GetNearbyPlaces(double latitude, double longitute, string fieldType = "BasicFields", double radius = 5.0);
        Task<Details> GetPlaceInformation(string placeID);
        Task<string> ConvertToAddress(double latitude, double longitude);
    }
}
