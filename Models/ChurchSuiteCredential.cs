using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Models
{
    /// <summary>
    /// Stores ChurchSuite OAuth credentials for a user on a per-account basis.
    /// This allows users to authenticate with ChurchSuite and sync events.
    /// </summary>
    [Table("ChurchSuiteCredentials")]
    public class ChurchSuiteCredential
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The user ID from AspNetUsers table
        /// </summary>
        [Required]
        public string UserId { get; set; }

        /// <summary>
        /// ChurchSuite account subdomain (e.g., "myaccount" from "myaccount.churchsuite.com")
        /// </summary>
        [Required]
        [StringLength(255)]
        public string Subdomain { get; set; }

        /// <summary>
        /// OAuth access token for API calls
        /// </summary>
        [Required]
        public string AccessToken { get; set; }

        /// <summary>
        /// OAuth refresh token for obtaining new access tokens
        /// </summary>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// UTC timestamp when the access token expires
        /// </summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// ChurchSuite account ID/UUID (for reference)
        /// </summary>
        public string? AccountId { get; set; }

        /// <summary>
        /// The client_id used for this credential (per-user client id)
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Temporary PKCE code_verifier which is stored during the authorization handshake
        /// and used for the subsequent token exchange.
        /// NOTE: nullable; remove after successful token exchange if desired.
        /// </summary>
        public string? TempCodeVerifier { get; set; }

        /// <summary>
        /// When this credential was created/first linked
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this credential was last updated
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// When the token was last refreshed
        /// </summary>
        public DateTime? LastRefreshedAt { get; set; }

        /// <summary>
        /// Track if this credential is still valid/active
        /// </summary>
        [Required]
        public bool IsActive { get; set; } = true;
    }
}
