using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace ForestChurches.Models
{
    public class ServiceTimes
    {
        [ValidateNever]
        [Column("ServiceID")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Time { get; set; }

        [Required]
        public string Note { get; set; }

        // Foreign key to ChurchInformation
        [ValidateNever]
        [ForeignKey("ChurchInformationId")]
        public string ChurchInformationId { get; set; }

        [ValidateNever]
        public ChurchInformation ChurchInformation { get; set; }
    }
}
