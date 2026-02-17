using AzureProvisioningEngine.Models;
using System.Threading.Tasks;

namespace AzureProvisioningEngine.Services
{
    public interface IAzureProvisioningService
    {
        Task<AppRegistrationResult> ProvisionApplicationAsync(AppRegistrationRequest request);
    }
}
