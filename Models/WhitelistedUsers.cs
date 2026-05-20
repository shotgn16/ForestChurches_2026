using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace ForestChurches.Models
{
    public class WhitelistedUsers
    {
        [Column("ID")]
        public string ID { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Status")]
        public string Status { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "DateOfRegistration")]
        public DateTime DateOfRegistration { get; set; }

        [DataType(DataType.Text)]
        [Display(Name = "ExpiryDate")]
        public DateTime ExpiryDate { get; set; }
    }
}
