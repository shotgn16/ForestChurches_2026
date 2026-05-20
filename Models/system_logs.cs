using System.ComponentModel.DataAnnotations.Schema;

namespace ForestChurches.Models
{
    public class system_logs
    {
        [Column("Id")]
        public string Id { get; set; }
        public string Timestamp { get; set; }
        public string Level { get; set; }
        public string Exception { get; set; }
        public string RenderedMessage { get; set; }
        public string Url { get; set; }
        public string Action { get; set; }
    }
}
