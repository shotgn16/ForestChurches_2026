namespace ForestChurches.Models
{
    public class LogFileModel
    {
        public string Path { get; set; }
        public DateOnly ImportDate { get; set; }
        public TimeOnly ImportTime { get; set; }
    }
}
