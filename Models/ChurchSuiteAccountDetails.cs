using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ForestChurches.Models
{
    public class ChurchSuiteAccountDetails
    {
        // /account (api.churchsuite.com/v2) returns the account under data with these properties
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("uuid")]
        public string? Uuid { get; set; }

        [JsonPropertyName("subdomain")]
        public string? Subdomain { get; set; }

        [JsonPropertyName("timezone")]
        public string? Timezone { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        // common fields that may appear in both /account and OAuth /oauth/me responses
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("give2_keyword")]
        public string? Give2Keyword { get; set; }

        [JsonPropertyName("first_day_of_week")]
        public string? FirstDayOfWeek { get; set; }

        [JsonPropertyName("data_protection_title")]
        public string? DataProtectionTitle { get; set; }

        [JsonPropertyName("data_protection_body")]
        public string? DataProtectionBody { get; set; }

        [JsonPropertyName("data_protection_help")]
        public string? DataProtectionHelp { get; set; }

        [JsonPropertyName("email_open_tracking")]
        public bool EmailOpenTracking { get; set; }

        [JsonPropertyName("enforce_mfa")]
        public bool EnforceMfa { get; set; }

        [JsonPropertyName("billing_address")]
        public ChurchSuiteAddress? BillingAddress { get; set; }

        [JsonPropertyName("billing_region")]
        public string? BillingRegion { get; set; }

        [JsonPropertyName("billing_contact")]
        public Contact? BillingContact { get; set; }

        [JsonPropertyName("data_protection_contact")]
        public Contact? DataProtectionContact { get; set; }

        [JsonPropertyName("account_contact")]
        public Contact? AccountContact { get; set; }

        [JsonPropertyName("application")]
        public string? Application { get; set; }

        [JsonPropertyName("feature_flags")]
        public List<string>? FeatureFlags { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("created_by")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("modified_at")]
        public string? ModifiedAt { get; set; }

        [JsonPropertyName("modified_by")]
        public string? ModifiedBy { get; set; }
    }
}

