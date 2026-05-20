using OpenTelemetry.Trace;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Models
{
    public class ChurchStatus
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string ID { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Closed";
        public string status { get; set; }
        public ChurchInformation ChurchInformation { get; set; }

        public ChurchStatus(string name)
        {
            Name = name;
            status = name; // Default Status 
        }

        public string Close()
        {
            if (status == "Open")
            {
                status = "Closed";
            }

            return status;
        }

        public string Open()
        {
            if (status == "Closed")
            {
                status = "Open";
            }

            return status;
        }

        public string NotCreated()
        {
            if (status != "NotCreated")
            {
                status = "NotCreated";
            }

            return status;
        }

        public string GetStatus()
        {
            return status;
        }
    }
}
