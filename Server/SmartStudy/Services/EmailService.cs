using System.Net;
using System.Net.Mail;

namespace SmartStudy.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"];
        var port = smtp["Port"];
        var username = smtp["Username"];
        var from = smtp["From"] ?? "noreply@smartstudy.com";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username))
        {
            // SMTP not configured — log instead of sending
            Console.WriteLine($"[EmailService] Would send email to {to}: {subject}\n{body}");
            return;
        }

        using var client = new SmtpClient(host, int.Parse(port ?? "587"));
        client.Credentials = new NetworkCredential(username, smtp["Password"]);
        client.EnableSsl = true;

        var msg = new MailMessage(from, to, subject, body) { IsBodyHtml = false };
        await client.SendMailAsync(msg);
    }
}
