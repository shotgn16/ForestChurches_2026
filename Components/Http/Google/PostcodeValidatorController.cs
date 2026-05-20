using Microsoft.AspNetCore.Mvc;

namespace ForestChurches.Components.Http.Google
{
    public class PostcodeValidatorController : Controller, PostcodeValidatorInterface
    {
        List<string> codes;

        public PostcodeValidatorController()
        {
            codes = new List<string>()
                { "GL14", "GL15", "GL16", "GL17", "GL18", "GL19", "HR8", "HR9", "NP16", "NP25", "WR13" };
        }

        public async Task<bool> ValidatePostcodeAsync(string postcode)
        {
            bool result = false;
            string substring = await getSubstringAsync(postcode);

            if (codes.Contains(substring))
            {
                result = true;
            }

            else if (!codes.Contains(substring))
            {
                throw new Exception("Please provide a postcode within the Forest Of Dean & Wye Valley to continue...");
            }

            return result;
        }

        private async Task<string> getSubstringAsync(string postcode)
        {
            string result = null;

            if (!string.IsNullOrEmpty(postcode))
            {
                result = postcode.Substring(0, 4);
            }

            return result;
        }
    }
}
