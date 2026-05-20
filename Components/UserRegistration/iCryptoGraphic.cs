namespace ForestChurches.Components.UserRegistration
{
    public interface iCryptoGraphic
    {
        byte[] Hash(string input, byte[] secretKey);
    }
}
