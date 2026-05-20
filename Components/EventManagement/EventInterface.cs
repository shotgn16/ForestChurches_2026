using ForestChurches.Components.Users;
using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;
using Org.BouncyCastle.Crypto.Paddings;

namespace ForestChurches.Components.AutoEvents
{
    public interface EventInterface
    {
        // This method will pull all events from the database
        // It will only pull events which are NOT marked for repeat...
        Task<object> GetEvents();
        Task<object> GetRepeatedEvents();

        // This method will iterate through the events and sort/delete those which have experied date/times (+24 hours)
        Task<object> SortAndDeleteEvents();

        // This method will check repeated events and email users to ask if they still want events
        Task InformUsersAndDeleteEvents(ChurchAccount User, List<EventsModel> eventsToDelete);

        // This method will check (monthly) if the user still needs the events that are registered to repeat
        Task<object> CheckRepeatedEvents();
        
        //
        Task UpdateEventAsync(ChurchAccount User, EventsModel input, Byte[] Image);
        //Guid GetEventID(EventsModel input);
    }
}
