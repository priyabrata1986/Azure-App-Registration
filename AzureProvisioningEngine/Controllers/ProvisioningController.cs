using AzureProvisioningEngine.Models;
using AzureProvisioningEngine.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace AzureProvisioningEngine.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProvisioningController : ControllerBase
    {
        private readonly IAzureProvisioningService _provisioningService;
        private readonly INotificationService _notificationService;
        private readonly ILogger<ProvisioningController> _logger;

        public ProvisioningController(
            IAzureProvisioningService provisioningService, 
            INotificationService notificationService,
            ILogger<ProvisioningController> logger)
        {
            _provisioningService = provisioningService;
            _notificationService = notificationService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterApplication([FromBody] AppRegistrationRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // 1. Notify Request Initiation
                string requestId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                await _notificationService.SendRequestInitiatedEmailAsync(request.OwnerEmail, request.AppName, requestId);

                // 2. Perform Provisioning
                var result = await _provisioningService.ProvisionApplicationAsync(request);

                // 3. Notify Completion
                await _notificationService.SendProvisioningCompletedEmailAsync(request.OwnerEmail, request.AppName, result.AppId, result.Status);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to provision application.");
                return StatusCode(500, new { Message = "An error occurred while provisioning the application.", Details = ex.Message });
            }
        }
    }
}
