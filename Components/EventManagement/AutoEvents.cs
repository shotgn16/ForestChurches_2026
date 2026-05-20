using ForestChurches.Components.AutoEvents;
using Quartz;

namespace ForestChurches.Components.EventManagement
{
    public class AutoEvents : IJob
    {
        private readonly EventInterface _eventInterface;
        private readonly ILogger<AutoEvents> _logger;
        public AutoEvents(EventInterface eventInterface, ILogger<AutoEvents> logger)
        {
            _eventInterface = eventInterface;
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var data = context.JobDetail.JobDataMap;

            try
            {
                // Sort through & Delete 'one-time' events
                var result = await _eventInterface.GetEvents();
                    _logger.LogInformation($"Process: 'GetEvents()' Complete! Status: {result}");

                var result1 = await _eventInterface.SortAndDeleteEvents();
                    _logger.LogInformation($"Process: 'SortAndDeleteEvents()' Complete! Status: {result1}");

                // Sort through & Email administrators about 'repeated' events
                var result2 = await _eventInterface.GetRepeatedEvents();
                    _logger.LogInformation($"Process: 'GetRepeatedEvents()' Complete! Status: {result2}");

                var result3 = await _eventInterface.CheckRepeatedEvents();
                    _logger.LogInformation($"Process: 'CheckRepeatedEvents()' Complete! Status: {result3}");
            }
            catch (Exception ex)
            {
                // Log the error

            }
        }

    }
}
