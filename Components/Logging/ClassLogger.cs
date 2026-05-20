using Serilog;
using Serilog.Events;
using System;

namespace ForestChurches.Components.LogReader
{
    public class ClassLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly Serilog.ILogger _serilogLogger;

        public ClassLogger()
        {
            _serilogLogger = Serilog.Log.Logger; // assuming you have Serilog configured
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null; // not needed for Serilog
        }

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return _serilogLogger.IsEnabled((Serilog.Events.LogEventLevel)logLevel);
        }

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var message = formatter(state, exception);
            if (exception != null)
            {
                _serilogLogger.Write((Serilog.Events.LogEventLevel)logLevel, exception, message);
            }
            else
            {
                _serilogLogger.Write((Serilog.Events.LogEventLevel)logLevel, message);
            }
        }
    }
}