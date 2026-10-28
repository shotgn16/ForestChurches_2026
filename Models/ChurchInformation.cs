using ForestChurches.Components.Users;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Models
{
    public class ChurchInformation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [ValidateNever]
        public string ChurchAccountId { get; set; }

        [Required]
        public ChurchAccount ChurchAccount { get; set; }

        /// <summary>The benefice this church belongs to, if any. Independent of the managing account.</summary>
        [ValidateNever]
        public string? BeneficeId { get; set; }

        [ValidateNever]
        public Benefice? Benefice { get; set; }

        /// <summary>
        /// This church's own picture in Blob Storage. When empty, the managing account's picture is used
        /// (which is how single-church accounts have always worked).
        /// </summary>
        [ValidateNever]
        public string? ImageBlobName { get; set; }

        [Required]
        public ICollection<ServiceTimes> ServiceTimes { get; set; } = new List<ServiceTimes>();

        [Required]
        [StringLength(30, ErrorMessage = "Church cannot exceed 30 characters")]
        public string Name { get; set; } = "Default Value";

        [Required]
        [StringLength(1000, ErrorMessage = "Description can't be longer than 1,000 characters")]
        public string Description { get; set; } = "Please add your church description here. Please use no more than 500 characters.";

        [Required]
        [Display(Name = "Status")]
        public string Status { get; set; } = "Closed";

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Denomination")]
        public string Denomination { get; set; } = "Default Value";

        [Required]
        [DataType(DataType.Text)]
        [Display(Name = "Congregation")]
        public string Congregation { get; set; } = "0-10";

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

        /// <summary>
        /// This subdomain string used for this church on ChurchSuite (e.g., "kingshope" if kingshope.churchsuite.com)
        /// </summary>
        [DataType(DataType.Text)]
        public string? ChurchSuiteSubdomain { get; set; }

        /// <summary>
        /// The X-Auth-Key configuration token used for server-to-server Profile synchronization
        /// </summary>
        [DataType(DataType.Text)]
        public string? ChurchSuiteApiKey { get; set; }
    }
}
