namespace ForestChurches.Components.Email
{
    public interface iEmail
    {
        Task StartEmailAsync(string recipientEmail, Dictionary<string, string> userData, string subject, string templatePath);
        Task SendEmailAsync(string recipientEmail, string emailContent, string subject);
    }
}
