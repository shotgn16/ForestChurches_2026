using ForestChurches.Components.Email;
using ForestChurches.Components.Users;
using ForestChurches.Data;
using ForestChurches.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ForestChurches.Components.AutoEvents
{
    public class EventContoller : EventInterface
    {
        // Dependency Injection
        private ILogger<EventContoller> _logger;
        private ForestChurchesContext _context;
        private UserManager<ChurchAccount> _userManager;
        private readonly iMailSender _mailSender;

        // Property Delcaration
        public List<EventsModel> Events { get; set; }
        public List<EventsModel> EventsToDelete { get; set; }
        public List<EventsModel> RepeatedEvents { get; set; }
        public List<EventsModel> RepeatedEventsToDelete { get; set; }

        public EventContoller(ForestChurchesContext context, iMailSender mailSender, UserManager<ChurchAccount> userManager, ILogger<EventContoller> logger)
        {
            _logger = logger;
            _context = context;
            _mailSender = mailSender;
            _userManager = userManager;

            // Call the GetEvents method to populate the Events property
            GetEvents();
        }

        public async Task<object> GetEvents()
        {
            Events = _context.Events
                .Where(a => a.Repeats == "None")
                .ToList();

            return new OkObjectResult(Task.CompletedTask);
        }

        public async Task<object> GetRepeatedEvents()
        {
            try
            {
               RepeatedEvents = _context.Events
                .Where(a => a.Repeats != "None")
                .ToList();
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return new BadRequestObjectResult(Task.FromException(ex));
            }

            return new OkObjectResult(Task.CompletedTask);
        }

        // Task : Sort and delete events
        public async Task<object> SortAndDeleteEvents()
        {
            try
            {
                foreach (var item in Events)
                {
                    var tempDateTime = item.Date.ToDateTime(item.EndTime);

                    if (DateTime.Now.AddHours(24) >= tempDateTime)
                    {
                        EventsToDelete.Add(item);
                    }
                }

                if (EventsToDelete != null && EventsToDelete.Count > 0)
                {
                    await InformUsersAndDeleteEvents(_context.ChurchAccount.Where(a => a.Email == EventsToDelete[0].User).FirstOrDefault(), EventsToDelete);
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return new BadRequestObjectResult(Task.FromException(ex));
            }

            return new OkObjectResult(Task.CompletedTask);
        }

        // Task : Check auto-event management works
        public async Task<object> CheckRepeatedEvents()
        {
            try
            {
                if (RepeatedEvents != null && RepeatedEvents.Count > 0)
                {
                    foreach (var item in RepeatedEvents)
                    {
                        var User = await _userManager.FindByNameAsync(item.User).ConfigureAwait(true);

                        var tempDateTime = item.Date.ToDateTime(item.EndTime);

                        if (DateTime.Now.AddDays(90) >= tempDateTime)
                        {
                            _mailSender.SendEmailEventValidate(User.Email, User.UserName, "/Events", item);
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                return new BadRequestObjectResult(Task.FromException(ex));
            }

            return new OkObjectResult(Task.CompletedTask);
        }

        // Task : Inform users and delete events
        public async Task InformUsersAndDeleteEvents(ChurchAccount User, List<EventsModel> eventsToDelete)
        {
            foreach (var item in eventsToDelete)
            {
                _context.Events.Remove(item);

                _mailSender.SendEmailEventDeleted(User.Email, User.Email, "/Events", item);
            }

            _context.SaveChanges();
        }

        // SWITCHING TO DIFFERENT TYPE OF FUNCTIONS NOW

        public async Task UpdateEventAsync(ChurchAccount User ,EventsModel input, Byte[] Image)
        {
            try
            {
                var currentChurch = _context.ChurchInformation.Where(a => a.ChurchAccountId == User.Id).FirstOrDefault();

                _context.Events.Where(a => a.ID == input.ID).ExecuteUpdate(setters => setters
                    .SetProperty(p => p.Name, input.Name)
                    .SetProperty(p => p.User, User.Email)
                    .SetProperty(p => p.Church, currentChurch.Name)
                    .SetProperty(p => p.Repeats, input.Repeats)
                    .SetProperty(p => p.Address, input.Address)
                    .SetProperty(p => p.StartTime, input.StartTime)
                    .SetProperty(p => p.EndTime, input.EndTime)
                    .SetProperty(p => p.Date, input.Date)
                    .SetProperty(p => p.Description, input.Description)
                    .SetProperty(p => p.Link, input.Link)
                );

                // Only replace the picture when a new one was supplied; editing other details keeps the current image.
                if (Image != null && Image.Length > 0)
                {
                    _context.Events.Where(a => a.ID == input.ID).ExecuteUpdate(setters => setters
                        .SetProperty(p => p.ImageArray, Image));
                }

                _context.SaveChanges();
            }
             
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
                throw ex;
            }
        }
    }
}
