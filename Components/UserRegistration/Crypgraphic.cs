using System.Security.Cryptography;
using System.Text;

namespace ForestChurches.Components.UserRegistration
{
    public class Crypgraphic : iCryptoGraphic
    {
        public byte[] Hash(string input, byte[] secretKey)
        {
            using (var hmac = new HMACSHA256())
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
            }
        }
    }
}
