using ForestChurches.Models;

namespace ForestChurches.Components.Email
{
    public interface iMailSender
    {
        void SendEmailEventDeleted(string email, string un, string redirectUrl, EventsModel deletedEvent);
        void SendEmailEventCreated(string email, string redirectUrl, string repeats ,EventsModel addedEvent);
        void SendEmailContactRequest(string email, string un, string recipientEmail, string senderEmail, string msg, string redirectUrl);
        Task SendEmailAdminContactRequest(string recipientUsername, string recipientEmail, string msg, string senderName);
        void SendEmailConfirmAccount(string un, string redirectUrl);
        void SendNewEmailConfirmation(string un, string redirectUrl);
        void SendEmailRegisteredUser(string email, string un, string redirectUrl);
        void SendEmailEventValidate(string email, string un, string redirectUrl, EventsModel eventItem);
        Task SendEmailResetPassword(string email, string redirectUrl);
        void SendEmailChurchInformation(string recipientEmail, ChurchInformation church, string serviceTimes);
        Task SendEmailInviteUser(string invitedEmail, string redirectUrl);
        string ValidateToken(string token);
    }
}
