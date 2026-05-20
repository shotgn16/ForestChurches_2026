using System.ComponentModel.DataAnnotations;

namespace ForestChurches.Models
{
    public class Address
    {
        [Required]
        public string Street { get; set; }

        [Required]
        public string City { get; set; }

        [Required]
        public string State { get; set; }

        [Required]
        [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Invalid postal code format.")]
        public string PostalCode { get; set; }

        [Required]
        public string Country { get; set; }
    }
}
