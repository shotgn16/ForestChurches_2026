using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ForestChurches.Models
{
    /// <summary>
    /// A benefice: a group of churches served together (usually by one parish team).
    /// Churches link to a benefice independently of which account manages them, so a church
    /// can be handed to its own administrator (moved to another account) and stay in the benefice.
    /// </summary>
    public class Benefice
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [StringLength(120, ErrorMessage = "The benefice name can't be longer than 120 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "The description can't be longer than 1,000 characters")]
        public string? Description { get; set; }

        [DataType(DataType.Url)]
        [StringLength(300)]
        public string? Website { get; set; }

        [ValidateNever]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        [ValidateNever]
        public ICollection<ChurchInformation> Churches { get; set; } = new List<ChurchInformation>();
    }
}
