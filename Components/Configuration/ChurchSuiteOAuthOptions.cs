namespace ForestChurches.Components.Configuration
{
    /// <summary>
    /// Configuration options for ChurchSuite OAuth integration.
    /// </summary>
    public class ChurchSuiteOAuthOptions
    {
        /// <summary>
        /// The OAuth Client ID for ChurchSuite application.
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// The OAuth Client Secret for ChurchSuite application (optional, used for confidential clients).
        /// </summary>
        public string ClientSecret { get; set; }
    }
}
