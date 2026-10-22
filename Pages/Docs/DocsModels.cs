using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

// Page models for the documentation at /docs. The pages are static content; the models exist
// to carry the access rules:
//   /docs, /docs/visitors, /docs/church-admins   anyone (no sign-in)
//   /docs/site-admins                             accounts with the Admin.Read permission (same as Site admin)
//   /docs/system/...                              SuperAdmin only: architecture, keys, security and data handling
namespace ForestChurches.Pages.Docs
{
    public class DocsPublicModel : PageModel
    {
        public void OnGet() { }
    }

    [Authorize(Policy = "Admin.Read")]
    public class DocsSiteAdminModel : PageModel
    {
        public void OnGet() { }
    }

    [Authorize(Roles = "SuperAdmin")]
    public class DocsSystemModel : PageModel
    {
        public void OnGet() { }
    }
}
