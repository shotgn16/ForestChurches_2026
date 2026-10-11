using System.Text.Json.Serialization;

namespace ForestChurches.Models
{
    public class ChurchSuiteEventDto
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; } // Can be integer or string depending on version setup

        [JsonPropertyName("identifier")]
        public string? Identifier { get; set; }

        [JsonPropertyName("sequence_id")]
        public int? SequenceId { get; set; }

        [JsonPropertyName("brand_id")]
        public int? BrandId { get; set; }

        [JsonPropertyName("booking_id")]
        public int? BookingId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        // v2 API fields
        [JsonPropertyName("starts_at")]
        public string? StartsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public string? EndsAt { get; set; }

        // Legacy fields (v1 API)
        [JsonPropertyName("datetime_start")]
        public string? DateTimeStart { get; set; }

        [JsonPropertyName("datetime_end")]
        public string? DateTimeEnd { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("category_id")]
        public int? CategoryId { get; set; }

        [JsonPropertyName("visibility")]
        public VisibilityDto? Visibility { get; set; }

        [JsonPropertyName("signup_options")]
        public SignupOptionsDto? SignupOptions { get; set; }

        [JsonPropertyName("image")]
        public ImageDto? Image { get; set; }

        // Location can be an object or string depending on the API version
        [JsonPropertyName("location")]
        public LocationDto? Location { get; set; }

        [JsonPropertyName("all_sites")]
        public bool? AllSites { get; set; }

        [JsonPropertyName("site_ids")]
        public List<int>? SiteIds { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }

        [JsonPropertyName("created_by")]
        public string? CreatedBy { get; set; }

        [JsonPropertyName("created_source")]
        public string? CreatedSource { get; set; }

        [JsonPropertyName("modified_at")]
        public string? ModifiedAt { get; set; }

        [JsonPropertyName("modified_by")]
        public string? ModifiedBy { get; set; }

        [JsonPropertyName("venue")]
        public string? Venue { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("image_thumb")]
        public string? ImageThumb { get; set; }
    }

    public class LocationDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    public class ImageDto
    {
        [JsonPropertyName("thumbnail")]
        public string? Thumbnail { get; set; }

        [JsonPropertyName("small")]
        public string? Small { get; set; }

        [JsonPropertyName("medium")]
        public string? Medium { get; set; }

        [JsonPropertyName("large")]
        public string? Large { get; set; }

        [JsonPropertyName("uploaded_at")]
        public string? UploadedAt { get; set; }
    }

    public class VisibilityDto
    {
        [JsonPropertyName("my")]
        public VisibilityDetailDto? My { get; set; }

        [JsonPropertyName("embed")]
        public bool? Embed { get; set; }

        [JsonPropertyName("connect")]
        public bool? Connect { get; set; }
    }

    public class VisibilityDetailDto
    {
        [JsonPropertyName("visible")]
        public bool? Visible { get; set; }

        [JsonPropertyName("visible_to")]
        public List<VisibleToDto>? VisibleTo { get; set; }

        [JsonPropertyName("featured_events")]
        public bool? FeaturedEvents { get; set; }
    }

    public class VisibleToDto
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("module")]
        public string? Module { get; set; }
    }

    public class SignupOptionsDto
    {
        [JsonPropertyName("capacity")]
        public int? Capacity { get; set; }

        [JsonPropertyName("sequence_signup")]
        public bool? SequenceSignup { get; set; }

        [JsonPropertyName("my_signup")]
        public bool? MySignup { get; set; }

        [JsonPropertyName("embed_signup")]
        public bool? EmbedSignup { get; set; }

        [JsonPropertyName("allow_cancel")]
        public bool? AllowCancel { get; set; }

        [JsonPropertyName("confirmation_email")]
        public ConfirmationEmailDto? ConfirmationEmail { get; set; }
    }

    public class ConfirmationEmailDto
    {
        [JsonPropertyName("sender_email")]
        public string? SenderEmail { get; set; }

        [JsonPropertyName("sender_name")]
        public string? SenderName { get; set; }

        [JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }

    public class ChurchSuiteEventResponse
    {
        [JsonPropertyName("data")]
        public List<ChurchSuiteEventDto>? Data { get; set; }

        [JsonPropertyName("pagination")]
        public PaginationDto? Pagination { get; set; }
    }

    public class PaginationDto
    {
        [JsonPropertyName("num_results")]
        public int NumResults { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("next_page")]
        public int? NextPage { get; set; }

        [JsonPropertyName("prev_page")]
        public int? PrevPage { get; set; }
    }
}
