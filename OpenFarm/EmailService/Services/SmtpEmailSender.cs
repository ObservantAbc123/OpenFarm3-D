using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace EmailService.Services;

public class SmtpEmailSender(ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var server = Environment.GetEnvironmentVariable("SMTP_SERVER") ??
                     throw new ArgumentException("SMTP_SERVER not set");
        var portStr = Environment.GetEnvironmentVariable("SMTP_PORT") ??
                      throw new ArgumentException("SMTP_PORT not set");
        if (!int.TryParse(portStr, out var port)) throw new ArgumentException("SMTP_PORT invalid");
        var senderName = Environment.GetEnvironmentVariable("SENDER_NAME") ??
                         throw new ArgumentException("SENDER_NAME not set");
        var email = Environment.GetEnvironmentVariable("GMAIL_EMAIL") ??
                    throw new ArgumentException("GMAIL_EMAIL not set");
        var password = Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD") ??
                       throw new ArgumentException("GMAIL_APP_PASSWORD not set");

        using var client = new SmtpClient();
        client.Host = server;
        client.Port = port;
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.UseDefaultCredentials = false;
        client.EnableSsl = true;
        client.Credentials = new NetworkCredential(email, password);

        using var message = new MailMessage();
        message.From = new MailAddress(email, senderName);
        message.Subject = subject;
        message.To.Add(new MailAddress(to));

        var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);
        message.AlternateViews.Add(htmlView);

        await client.SendMailAsync(message, ct);
        logger.LogInformation("Email sent to {Email}", to);
    }
}