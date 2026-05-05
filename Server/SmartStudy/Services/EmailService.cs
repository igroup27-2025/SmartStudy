using System.Net;
using System.Net.Mail;

namespace SmartStudy.Services;

// Sends transactional emails via SMTP — used for password-reset codes and similar.
public class EmailService
{
    private readonly IConfiguration _config;

    // Injects configuration to read SMTP settings.
    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    // True when host and username are both set in appsettings.
    public bool IsConfigured
    {
        get
        {
            var smtp = _config.GetSection("Smtp");
            return !string.IsNullOrEmpty(smtp["Host"]) && !string.IsNullOrEmpty(smtp["Username"]);
        }
    }

    // Sends an email or, if SMTP is not configured, logs a masked summary to console.
    public async Task SendAsync(string to, string subject, string body)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"];
        var port = smtp["Port"];
        var username = smtp["Username"];
        var from = smtp["From"] ?? "noreply@smartstudy.com";

        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(username))
        {
            // SMTP not configured — log masked summary instead of full body
            var maskedBody = body.Length > 40 ? body[..40] + "..." : body;
            Console.WriteLine($"[EmailService] Would send email to {to}: {subject} (body: {maskedBody})");
            return;
        }

        using var client = new SmtpClient(host, int.Parse(port ?? "587"));
        client.Credentials = new NetworkCredential(username, smtp["Password"]);
        client.EnableSsl = true;

        var msg = new MailMessage(from, to, subject, body) { IsBodyHtml = false };
        await client.SendMailAsync(msg);
    }
}
