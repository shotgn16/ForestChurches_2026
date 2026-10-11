using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

namespace ForestChurches.Models
{
    public class Profile
    {
        [Required]
        [DataType(DataType.EmailAddress)]
        public string Email { get; set; }

        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; }

        [ValidateNever]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [AllowNull]
        [DataType(DataType.MultilineText)]
        public List<string> Roles { get; set; }

        // Indicates whether the account is active/confirmed in the system.
        public bool IsActive { get; set; }
    }
}
