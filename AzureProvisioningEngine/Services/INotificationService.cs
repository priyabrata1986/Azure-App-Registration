using System.Threading.Tasks;

namespace AzureProvisioningEngine.Services
{
    public interface INotificationService
    {
        Task SendRequestInitiatedEmailAsync(string toEmail, string appName, string requestId);
        Task SendProvisioningCompletedEmailAsync(string toEmail, string appName, string appId, string status);
    }
}
