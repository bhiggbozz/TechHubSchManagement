using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using TechHub.Core.Utilities;

namespace TechHub.Service.Service
{
    //public class EmailService : IEmailService
    //{
    //    private readonly IEmailService _realEmailService;
    //   // private readonly IRepository<SentEmail> _emailRepository;

    //    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    //    {
    //        var sentEmail = new SentEmail
    //        {
    //            Id = Guid.NewGuid(),
    //            ToEmail = to,
    //            Subject = subject,
    //            Body = body,
    //            IsHtml = isHtml,
    //            SentDate = DateTime.UtcNow
    //        };

    //        try
    //        {
    //            await _realEmailService.SendEmailAsync(to, subject, body, isHtml);
    //            sentEmail.Status = "Sent";
    //        }
    //        catch (Exception ex)
    //        {
    //            sentEmail.Status = "Failed";
    //            sentEmail.ErrorMessage = ex.Message;
    //            throw;
    //        }
    //        finally
    //        {
    //            await _emailRepository.CreateAsync(sentEmail);
    //        }
    //    }
    }
