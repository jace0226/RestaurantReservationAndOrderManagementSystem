using MailKit.Net.Smtp;
using MailKit;
using MailKit.Security;
using MimeKit;
using System.Net.Sockets;
using System.IO;

namespace RestaurantReservationAndOrderManagementSystem.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            ILogger<EmailService> logger) {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string recipientEmail, string subject, string body) {
            try
            {
                var senderName = _configuration["EmailSettings:SenderName"];
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var host = _configuration["EmailSettings:Host"];
                var port = _configuration.GetValue<int>("EmailSettings:Port");

                if (string.IsNullOrWhiteSpace(senderEmail) ||
                    string.IsNullOrWhiteSpace(host) ||
                    port <= 0 ||
                    !MailboxAddress.TryParse(recipientEmail, out var recipient))
                {
                    _logger.LogWarning(
                        "Email was not sent because the SMTP configuration or recipient email is invalid.");
                    return;
                }

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(
                    string.IsNullOrWhiteSpace(senderName) ? "Restaurant" : senderName,
                    senderEmail));
                email.To.Add(recipient);
                email.Subject = subject;
                email.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(host, port, SecureSocketOptions.None);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
            }
            catch (SocketException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Email could not be sent to {RecipientEmail} because the SMTP server refused or could not accept the connection.",
                    recipientEmail);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Email could not be sent to {RecipientEmail} due to an SMTP connection I/O failure.",
                    recipientEmail);
            }
            catch (SmtpCommandException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Email could not be sent to {RecipientEmail} because the SMTP server rejected the request.",
                    recipientEmail);
            }
            catch (SmtpProtocolException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Email could not be sent to {RecipientEmail} due to an SMTP protocol error.",
                    recipientEmail);
            }
            catch (AuthenticationException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Email could not be sent to {RecipientEmail} because SMTP authentication failed.",
                    recipientEmail);
            }
        }
    }
}
