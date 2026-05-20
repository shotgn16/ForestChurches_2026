using ForestChurches.Components.Performance;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ForestChurches.Services
{
    public class ProcessUsageService : IProcessUsageService
    {
        private TimeSpan _lastTotalProcessorTime;
        private DateTime _lastTime;

        public ProcessUsageService()
        {
            using (var process = Process.GetCurrentProcess())
            {
                _lastTotalProcessorTime = process.TotalProcessorTime;
                _lastTime = DateTime.UtcNow;
            }
        }

        public async Task<float> GetCpuUsagePercentageAsync()
        {
            await Task.Delay(500); // Wait for a short period to calculate CPU usage

            using (var process = Process.GetCurrentProcess())
            {
                var currentTotalProcessorTime = process.TotalProcessorTime;
                var currentTime = DateTime.UtcNow;

                var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
                var totalMsPassed = (currentTime - _lastTime).TotalMilliseconds;

                var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

                // Update the last recorded times
                _lastTotalProcessorTime = currentTotalProcessorTime;
                _lastTime = currentTime;

                return (float)(cpuUsageTotal * 100);
            }
        }

        public float GetMemoryUsageInMB()
        {
            using (var process = Process.GetCurrentProcess())
            {
                return process.WorkingSet64 / (1024f * 1024f); // Convert bytes to megabytes
            }
        }
    }
}