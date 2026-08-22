
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Utilities;
// OR
using TechHub.Service.Interface;
//using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace TechHub.Service.Service;

public class EmailService : IEmailService
{
    private const string MailtrapSendUrl = "https://send.api.mailtrap.io/api/send";

    private static readonly HttpClient _httpClient = new();

    //private readonly IEmailService _realEmailService;
	private readonly IQueryRepository<EmailTemplate>_templateRepository;
	private readonly IConfiguration _configuration;
	private readonly ILogger _logger;

	private readonly string _fromEmail;
	private readonly string _fromName;
	// private readonly IRepository<SentEmail> _emailRepository;
	public EmailService( IQueryRepository<EmailTemplate> templateRepository, IConfiguration configuration, ILogger logger)
    {
       // _realEmailService = realEmailService;
        _templateRepository = templateRepository;
        _configuration = configuration;
        _logger = logger;

		_fromEmail = _configuration["EmailSettings:FromEmail"] ?? throw new ArgumentNullException("EmailSettings:FromEmail is not configured");

		_fromName = _configuration["EmailSettings:FromName"] ?? "TechHub";
	}

	public async Task<string> GetRenderedTemplate(int key, Dictionary<string, string> placeholders)
	{
		var query = $@"SELECT * FROM EmailTemplates 
                      WHERE TemplateKey = '{key}' 
                      AND   IsActive    = 1";
		var template = await _templateRepository.GetByQuery(query);

		if (template is null || !template.Any())
		{
			_logger.Warning(
				"Email template not found - TemplateKey: {Key}", key);
			return null;
		}

		var html = template.First().HtmlBody;

		if (string.IsNullOrWhiteSpace(html))
			return null;

		// ✅ placeholder.Key already contains @@ prefix
		// just replace it directly — no curly braces
		foreach (var placeholder in placeholders)
		{
			html = html.Replace(placeholder.Key, placeholder.Value);
		}

		return html;
	}

	//public async Task SendAsync(string toEmail,string toName,string subject,string htmlBody)
	//{
	public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
	{
		var provider = _configuration["EmailSettings:Provider"] ?? "Smtp";

		if (provider.Equals("MailtrapApi", StringComparison.OrdinalIgnoreCase))
		{
			await SendViaMailtrapApiAsync(toEmail, toName, subject, htmlBody);
			return;
		}

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
				_logger.Warning("Mailtrap API settings incomplete; skipping send to {Email}", toEmail);
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
			request.Headers.UserAgent.ParseAdd("TechHub/1.0");
			request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

			using var response = await _httpClient.SendAsync(request);

			var body = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode)
			{
				_logger.Information("Email sent to {Email} Subject: {Subject} (HTTP {Status})",
					toEmail, subject, (int)response.StatusCode);
			}
			else
			{
				_logger.Warning(
					"Mailtrap API send failed to {Email} (HTTP {Status}): {Body}",
					toEmail, (int)response.StatusCode, body);
			}
		}
		catch (Exception ex)
		{
			_logger.Error(ex, "Failed to send email to {Email}", toEmail);
			// Don't throw — email failure should not block user creation.
		}
	}

	/// <summary>Sandbox / SMTP path — Mailtrap sandbox host or any SMTP relay.</summary>
	private async Task SendViaSmtpAsync(string toEmail, string toName, string subject, string htmlBody)
	{
		try
		{
			using var client = new SmtpClient(_configuration["EmailSettings:Host"], int.Parse(_configuration["EmailSettings:Port"]!))
			{
				Credentials = new NetworkCredential(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]),
				EnableSsl = true
			};

			var mailMessage = new MailMessage
			{
				From = new MailAddress(
					_configuration["EmailSettings:FromEmail"]!,
					_configuration["EmailSettings:FromName"]),
				Subject = subject,
				Body = htmlBody,
				IsBodyHtml = true
			};

			mailMessage.To.Add(new MailAddress(toEmail, toName));

			await client.SendMailAsync(mailMessage);

			_logger.Information("Email sent to {Email} Subject: {Subject}", toEmail, subject);
		}
		catch (Exception ex)
		{
			_logger.Error(
				ex,
				"Failed to send email to {Email}", toEmail);
		}
	}

	private string ReplacePlaceholders(string content,Dictionary<string, string> placeholders)
	{
		foreach (var placeholder in placeholders)
		{
			content = content.Replace(
				$"{{{{{placeholder.Key}}}}}",
				placeholder.Value);
		}
		return content;
	}

	//public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
	//   {
	//       var sentEmail = new SentEmail
	//       {
	//           Id = Guid.NewGuid(),
	//           ToEmail = to,
	//           Subject = subject,
	//           Body = body,
	//           IsHtml = isHtml,
	//           SentDate = DateTime.UtcNow
	//       };

	//       try
	//       {
	//           await _realEmailService.SendEmailAsync(to, subject, body, isHtml);
	//           sentEmail.Status = "Sent";
	//       }
	//       catch (Exception ex)
	//       {
	//           sentEmail.Status = "Failed";
	//           sentEmail.ErrorMessage = ex.Message;
	//           throw;
	//       }
	//       finally
	//       {
	//           await _emailRepository.CreateAsync(sentEmail);
	//       }
	//   }


}