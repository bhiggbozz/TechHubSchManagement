using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Enum;

namespace TechHub.Service.Service
{
    public interface IEmailService
    {
		//Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
		//Task SendEmailAsync(List<string> to, string subject, string body, bool isHtml = true);
		Task<string> GetRenderedTemplate(EmailTemplateKey key, Dictionary<string, string> placeholders);
		Task SendAsync(string toEmail,string toName,string subject,string htmlBody);
	}
}

