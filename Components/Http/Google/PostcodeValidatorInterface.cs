namespace ForestChurches.Components.Http.Google
{
    public interface PostcodeValidatorInterface
    {
        Task<bool> ValidatePostcodeAsync(string postcode);
    }
}
