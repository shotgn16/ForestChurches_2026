namespace ForestChurches.Components.UserRegistration
{
    public interface iRegistrationGenerate
    {
        bool ValidateEmail(string userEmail);
        string GenerateUrl(HttpRequest request, string userEmail);
        void MarkAsCompleted(string userEmail, DateTime registeredDate);
    }
}
