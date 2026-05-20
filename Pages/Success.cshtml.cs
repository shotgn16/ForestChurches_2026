using ForestChurches.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ForestChurches.Pages
{
    public class SuccessModel : PageModel
    {
        // Make it a public property so it can be accessed in the Razor view
        public Models.SuccessModel SuccessInfo { get; private set; }

        public void OnGet(string message, string title)
        {
            SuccessInfo = new Models.SuccessModel
            {
                SuccessMessage = message,
                SuccessTitle = title
            };
        }
    }
}
