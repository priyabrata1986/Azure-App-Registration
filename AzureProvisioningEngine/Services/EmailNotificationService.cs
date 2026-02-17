using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace AzureProvisioningEngine.Services
{
    public class EmailNotificationService : INotificationService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailNotificationService> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _fromAddress;

        public EmailNotificationService(IConfiguration configuration, ILogger<EmailNotificationService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            
            // In a real scenario, these would come from appsettings.json
            _smtpServer = _configuration["Smtp:Host"] ?? "smtp.example.com";
            _smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
            _fromAddress = _configuration["Smtp:From"] ?? "noreply@servicecatalog.com";
        }

        public async Task SendRequestInitiatedEmailAsync(string toEmail, string appName, string requestId)
        {
            string subject = $"Request Initiated: Azure App Registration - {appName}";
            string body = GetRequestInitiatedTemplate(appName, requestId);
            await SendEmailAsync(toEmail, subject, body);
        }

        public async Task SendProvisioningCompletedEmailAsync(string toEmail, string appName, string appId, string status)
        {
            string subject = $"Provisioning Complete: Azure App Registration - {appName}";
            string body = GetProvisioningCompletedTemplate(appName, appId, status);
            await SendEmailAsync(toEmail, subject, body);
        }

        private async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                _logger.LogInformation($"Sending email to {to} with subject: {subject}");

                // Mock implementation for demonstration if SMTP settings are not valid
                if (_smtpServer == "smtp.example.com")
                {
                    _logger.LogWarning("SMTP not configured. Skipping actual email send.");
                    return;
                }

                using (var client = new SmtpClient(_smtpServer, _smtpPort))
                {
                    client.EnableSsl = true;
                    // client.Credentials = new NetworkCredential("username", "password"); // Configure as needed

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_fromAddress),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(to);

                    await client.SendMailAsync(mailMessage);
                }
                _logger.LogInformation("Email sent successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email.");
                // Don't throw, notification failure shouldn't break the provisioning flow
            }
        }

        private string GetRequestInitiatedTemplate(string appName, string requestId)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 5px; }}
        .header {{ background-color: #3f51b5; color: white; padding: 15px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ padding: 20px; }}
        .footer {{ font-size: 12px; color: #666; text-align: center; margin-top: 20px; }}
        .highlight {{ font-weight: bold; color: #3f51b5; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Request Received</h2>
        </div>
        <div class='content'>
            <p>Hello,</p>
            <p>Your request for a new Azure App Registration has been received.</p>
            <p><strong>Application Name:</strong> {appName}</p>
            <p><strong>Request ID:</strong> {requestId}</p>
            <p>Our automated engine is processing your request. You will receive another notification once provisioning is complete.</p>
        </div>
        <div class='footer'>
            <p>ServiceNow | Azure Automation Team</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GetProvisioningCompletedTemplate(string appName, string appId, string status)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 5px; }}
        .header {{ background-color: #4caf50; color: white; padding: 15px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ padding: 20px; }}
        .info-box {{ background-color: #f5f7fa; padding: 15px; border-left: 4px solid #4caf50; margin: 15px 0; }}
        .footer {{ font-size: 12px; color: #666; text-align: center; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h2>Provisioning Complete</h2>
        </div>
        <div class='content'>
            <p>Hello,</p>
            <p>Good news! Your Azure App Registration has been successfully provisioned.</p>
            
            <div class='info-box'>
                <p><strong>Application Name:</strong> {appName}</p>
                <p><strong>Application (Client) ID:</strong> {appId}</p>
                <p><strong>Status:</strong> {status}</p>
            </div>

            <p>If you requested a Client Secret, it has been securely synced to the <strong>Azure_App_Secrets</strong> safe in CyberArk.</p>
            <p>You can now configure your application using these credentials.</p>
        </div>
        <div class='footer'>
            <p>ServiceNow | Azure Automation Team</p>
        </div>
    </div>
</body>
</html>";
        }
    }
}
