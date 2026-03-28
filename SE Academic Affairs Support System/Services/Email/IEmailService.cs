namespace SE_Academic_Affairs_Support_System.Services.Email
{
    public interface IEmailService
    {
        Task SendTicketAsync(string toEmail, string fullName);
    }
}
