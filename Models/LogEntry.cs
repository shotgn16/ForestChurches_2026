using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Models
{
    public class LogEntry
    {
        [Key]
        public string Id { get; set; }
        public string Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Exception { get; set; }
        public string Template { get; set; }
        public string Properties { get; set; }
    }
}
