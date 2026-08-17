using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using TechSchPlatform.Service.Interfaces;

namespace TechSchPlatform.Service.Services;

/// <summary>
/// Email sending with two interchangeable providers, chosen by config:
///   - Provider = "MailtrapApi" → production Mailtrap Email Sending API (token-based).
///   - Provider = anything else / "Smtp" → SMTP (used for the Mailtrap sandbox / tests).
/// </summary>
public class EmailService : IEmailService
{
    private const string MailtrapSendUrl = "https://send.api.mailtrap.io/api/send";

    private static readonly HttpClient _httpClient = new();

    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var provider = _configuration["EmailSettings:Provider"] ?? "Smtp";

        if (provider.Equals("MailtrapApi", StringComparison.OrdinalIgnoreCase))
            await SendViaMailtrapApiAsync(toEmail, toName, subject, htmlBody);
        else
            await SendViaSmtpAsync(toEmail, toName, subject, htmlBody);
    }

    /// <summary>Production path — Mailtrap Email Sending API (token-based, not SMTP).</summary>
    private async Task SendViaMailtrapApiAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var apiToken = _configuration["EmailSettings:MailtrapApiToken"];
            var fromEmail = _configuration["EmailSettings:FromEmail"];
            var fromName = _configuration["EmailSettings:FromName"] ?? "TechHub";

            if (string.IsNullOrEmpty(apiToken) || string.IsNullOrEmpty(fromEmail))
            {
                _logger.LogWarning("Mailtrap API settings incomplete; skipping send to {Email}", toEmail);
                return;
            }

            var payload = new
            {
                from = new { email = fromEmail, name = fromName },
                to = new object[] { new { email = toEmail, name = toName } },
                subject,
                html = htmlBody,
                category = "TechHub"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, MailtrapSendUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            request.Headers.UserAgent.ParseAdd("TechSchPlatform/1.0");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);

            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Mailtrap API email sent to {Email} Subject: {Subject} (HTTP {Status})",
                    toEmail, subject, (int)response.StatusCode);
            }
            else
            {
                _logger.LogWarning(
                    "Mailtrap API send failed to {Email} (HTTP {Status}): {Body}",
                    toEmail, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Mailtrap API email to {Email}", toEmail);
            // Don't throw — email failure should not block user creation.
        }
    }

    /// <summary>Sandbox / SMTP path — Mailtrap sandbox host or any SMTP relay.</summary>
    private async Task SendViaSmtpAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var host = _configuration["EmailSettings:Host"];
            var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];
            var fromEmail = _configuration["EmailSettings:FromEmail"];
            var fromName = _configuration["EmailSettings:FromName"] ?? "TechHub";

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(fromEmail))
            {
                _logger.LogWarning("Email settings not configured; skipping send to {Email}", toEmail);
                return;
            }

            using var client = new SmtpClient(host, port)
            {
                Credentials = string.IsNullOrEmpty(username) ? null : new NetworkCredential(username, password),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(new MailAddress(toEmail, toName));

            await client.SendMailAsync(mailMessage);

            _logger.LogInformation("Email sent to {Email} Subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            // Don't throw — email failure should not block user creation.
        }
    }
}