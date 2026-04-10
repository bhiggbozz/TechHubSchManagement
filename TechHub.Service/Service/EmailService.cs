
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Entities;
using TechHub.Core.Enum;
using TechHub.Core.Utilities;
using TechHub.Service.Interface;
//using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace TechHub.Service.Service;

public class EmailService : IEmailService
{
    //private readonly IEmailService _realEmailService;
	private readonly IQueryRepository<EmailTemplate>_templateRepository;
	private readonly IConfiguration _configuration;
	private readonly ILogger _logger;
	// private readonly IRepository<SentEmail> _emailRepository;
	public EmailService( IQueryRepository<EmailTemplate> templateRepository, IConfiguration configuration, ILogger logger)
    {
       // _realEmailService = realEmailService;
        _templateRepository = templateRepository;
        _configuration = configuration;
        _logger = logger;
	}

	public async Task<string> GetRenderedTemplate(int key, Dictionary<string, string> placeholders)
	{
		var query = $@"SELECT * FROM EmailTemplates WHERE TemplateKey = '{key}' AND IsActive = 1";

		var template = await _templateRepository.GetByQuery(query);

		var html = template.First().HtmlBody;

		// Replace placeholders
		foreach (var placeholder in placeholders)
		{
			html = html.Replace(
				$"{{{{{placeholder.Key}}}}}",
				placeholder.Value);
		}

		return html;
	}
	public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
	{
		try
		{
			using var client = new SmtpClient(_configuration["EmailSettings:Host"],int.Parse(_configuration["EmailSettings:Port"]!))
			{
				Credentials = new NetworkCredential(_configuration["EmailSettings:Username"],_configuration["EmailSettings:Password"]),
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

			_logger.Information("Email sent to {Email} Subject: {Subject}",toEmail, subject);
		}
		catch (Exception ex)
		{
			_logger.Error(
				ex,
				"Failed to send email to {Email}",toEmail);
			// Don't throw — email failure
			// should not block user creation
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