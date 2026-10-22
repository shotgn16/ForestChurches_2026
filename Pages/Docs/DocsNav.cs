namespace ForestChurches.Pages.Docs
{
    /// <summary>A page in one of the help guides.</summary>
    public sealed record DocPage(string Page, string Title);

    /// <summary>One of the three help guides shown under /docs.</summary>
    public sealed record DocGuide(string Key, string Title, string Audience, bool AdminOnly, IReadOnlyList<DocPage> Pages);

    /// <summary>A screenshot in a help page. Files live in wwwroot/img/docs/screens.</summary>
    public sealed record DocFig(string File, string Alt, string Caption, bool Short = false);

    /// <summary>
    /// The table of contents for the help guides. Adding a page here adds it to the sidebar and
    /// to the previous/next links; the Razor page itself goes under Pages/Docs/{guide}/.
    /// </summary>
    public static class DocsNav
    {
        public static readonly DocGuide Visitors = new("visitors", "Visitor guide", "Anyone using the public website", false, new[]
        {
            new DocPage("/Docs/Visitors/Index", "Finding churches and events"),
            new DocPage("/Docs/Visitors/More", "Bulletin, settings and help"),
        });

        public static readonly DocGuide Churches = new("churches", "Church administrator guide", "People who look after a church's listing", false, new[]
        {
            new DocPage("/Docs/Churches/Index", "Getting started"),
            new DocPage("/Docs/Churches/Profile", "Your church's page"),
            new DocPage("/Docs/Churches/Events", "Events"),
            new DocPage("/Docs/Churches/ChurchSuite", "Connecting ChurchSuite"),
            new DocPage("/Docs/Churches/Account", "Your account and data"),
        });

        public static readonly DocGuide Admin = new("admin", "Website administrator guide", "Site administrators (Admin and SuperAdmin)", true, new[]
        {
            new DocPage("/Docs/Admin/Index", "Site administration"),
            new DocPage("/Docs/Admin/People", "Users, roles and invitations"),
            new DocPage("/Docs/Admin/Content", "Churches, events, bulletins and emails"),
            new DocPage("/Docs/Admin/Monitoring", "Monitoring and logs"),
            new DocPage("/Docs/Admin/Architecture", "How the system fits together"),
            new DocPage("/Docs/Admin/Registration", "Whitelisting, sign-up and sign-in"),
            new DocPage("/Docs/Admin/BackgroundJobs", "Hangfire and background services"),
            new DocPage("/Docs/Admin/Email", "The EmailManagement app"),
            new DocPage("/Docs/Admin/ChurchSuite", "ChurchSuite integration"),
            new DocPage("/Docs/Admin/Security", "Security, keys and data"),
            new DocPage("/Docs/Admin/Operations", "Configuration, deployment and runbooks"),
            new DocPage("/Docs/Admin/KnownIssues", "Known issues"),
        });

        public static readonly IReadOnlyList<DocGuide> All = new[] { Visitors, Churches, Admin };

        public static DocGuide? ForPage(string? page) =>
            All.FirstOrDefault(g => g.Pages.Any(p => string.Equals(p.Page, page, StringComparison.OrdinalIgnoreCase)));
    }
}
