using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace RestaurantReservationAndOrderManagementSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration) {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string recipientEmail, string subject, string body) {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]));

            email.To.Add(MailboxAddress.Parse(recipientEmail));
            email.Subject = subject;

            email.Body = new BodyBuilder
            {
                TextBody = body
            }.ToMessageBody();

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _configuration["EmailSettings:Host"],
                _configuration.GetValue<int>("EmailSettings:Port"),
                SecureSocketOptions.None);

            await smtp.SendAsync(email);

            await smtp.DisconnectAsync(true);
        }
    }
}
