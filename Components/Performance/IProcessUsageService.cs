namespace ForestChurches.Components.Performance
{
    public interface IProcessUsageService
    {
        Task<float> GetCpuUsagePercentageAsync();
        float GetMemoryUsageInMB();
    }
}
