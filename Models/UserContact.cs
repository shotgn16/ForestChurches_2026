using System.Security.Cryptography.X509Certificates;

namespace ForestChurches.Models
{
    public class UserContact
    {
        public string Name { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
        public string ClientEmail { get; set; }
        public string Cid { get; set; }
    }
}
