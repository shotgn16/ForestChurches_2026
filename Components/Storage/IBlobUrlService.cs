namespace ForestChurches.Components.Storage
{
    public interface IBlobUrlService
    {
        /// <summary>
        /// Get a read-only SAS URL for a blob valid for the specified time period.
        /// </summary>
        string GetReadSasUrl(string blobName, System.TimeSpan validFor);
    }   
}
