namespace ForestChurches.Areas.Identity.Data
{
    public interface IDatabaseService
    {
        Task<float> GetLogDatabaseSize();
        Task<float> GetDatabaseSize();
    }
}
