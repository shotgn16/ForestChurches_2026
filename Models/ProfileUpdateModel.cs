using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace ForestChurches.Models
{
    public class ProfileUpdateModel
    {
        [ValidateNever]
        public string ID { get; set; }

        [Required]
        [StringLength(30, ErrorMessage = "Church cannot exceed 30 characters")]
        public string Name { get; set; }

        [Required]
        [StringLength(1000, ErrorMessage = "Description can't be longer than 500 characters")]
        public string Description { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Denomination")]
        public string Denomination { get; set; }

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Congregation")]
        public string Congregation { get; set; }

        [Required]
        public string Address { get; set; } = "Default Value";

        [Required]
        [DataType(DataType.PhoneNumber)]
        public string Phone { get; set; } = "000-000-0000";

        [Required]
        [DataType(DataType.Url)]
        public Uri Churchsuite { get; set; } = new Uri("https://churchsuite.com/");

        [Required]
        [DataType(DataType.Url)]
        public Uri Website { get; set; } = new Uri("https://forestchurches.co.uk/");

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Parking")]
        public bool Parking { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Restrooms")]
        public bool Restrooms { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Wheelchair Access")]
        public bool WheelchairAccess { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Wifi")]
        public bool Wifi { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Refreshments")]
        public bool Refreshments { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Worship Hall")]
        public bool WorshipHall { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Nursery/Childcare")]
        public bool Childcare { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "MeetingHall (For Community Use)")]
        public bool MeetingHall { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Kitchen")]
        public bool Kitchen { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Outdoor Spaces")]
        public bool OutdoorSpaces { get; set; } = false;

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Hearing Aid Support")]
        public bool HearingAidSupport { get; set; } = false;
    }
}
