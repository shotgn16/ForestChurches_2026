using System;
using System.ComponentModel.DataAnnotations;

namespace ForestChurches.Models
{
    public class Bulletin
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = "Forest Churches Weekly Pew Sheet";

        [Required]
        [MaxLength(255)]
        public string ParishName { get; set; } = "Forest of Dean Benefice";

        public DateTime BulletinDate { get; set; }

        [MaxLength(255)]
        public string SundayName { get; set; } = "Sunday Gathering";

        public string? CollectPrayer { get; set; }

        [MaxLength(255)]
        public string? FirstReading { get; set; }

        [MaxLength(255)]
        public string? GospelReading { get; set; }

        public string? PastoralNote { get; set; }

        // Stored as JSON array or newline separated string
        public string? HymnsList { get; set; }

        // Stored as JSON array or newline separated string
        public string? NoticesList { get; set; }

        // Type of the bulletin content: Bulletin, Announcement, Message
        [MaxLength(64)]
        public string Type { get; set; } = "Announcement";

        [MaxLength(255)]
        public string? PdfFilename { get; set; }

        public bool IsActive { get; set; } = true;

        // Whether this bulletin should show the site-wide banner
        public bool BannerVisible { get; set; } = false;

        // Optional expiry for the banner; null = do not auto-expire
        public DateTime? BannerExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
