namespace RestaurantReservationAndOrderManagementSystem.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string recipientEmail, string subject, string body);
    }
}
