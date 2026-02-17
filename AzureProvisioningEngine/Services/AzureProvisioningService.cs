using Azure.Identity;
using AzureProvisioningEngine.Models;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Azure.Security.KeyVault.Secrets;

namespace AzureProvisioningEngine.Services
{
    public class AzureProvisioningService : IAzureProvisioningService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly ILogger<AzureProvisioningService> _logger;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public AzureProvisioningService(IConfiguration configuration, ILogger<AzureProvisioningService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
            
            // Using DefaultAzureCredential for simplicity and security (Managed Identity/Env Vars)
            // In a real scenario, tenantId, clientId, clientSecret might be read from config
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var credential = new DefaultAzureCredential();
            _graphClient = new GraphServiceClient(credential, scopes);
        }

        public async Task<AppRegistrationResult> ProvisionApplicationAsync(AppRegistrationRequest request)
        {
            _logger.LogInformation($"Starting provisioning for app: {request.AppName}");

            try
            {
                // 1. Create the Application object
                var application = new Application
                {
                    DisplayName = request.AppName,
                    SignInAudience = request.SignInAudience,
                    Web = new Microsoft.Graph.Models.WebApplication
                    {
                        RedirectUris = request.RedirectUris != null ? new List<string>(request.RedirectUris) : new List<string>()
                    },
                    Description = request.Description,
                    Notes = $"Owner: {request.OwnerEmail} | Justification: {request.BusinessJustification}"
                };

                var createdApp = await _graphClient.Applications.PostAsync(application);
                _logger.LogInformation($"App created with ObjectId: {createdApp.Id} and AppId: {createdApp.AppId}");

                string secretText = null;
                DateTimeOffset? secretEnd = null;
                string keyVaultSecretUri = null;

                // 2. Generate Client Secret if requested
                if (request.GenerateClientSecret)
                {
                    var passwordCredential = new PasswordCredential
                    {
                        DisplayName = "InitialSecret",
                        EndDateTime = DateTimeOffset.UtcNow.AddYears(1) // Default 1 year validity
                    };

                    // Correct syntax for Microsoft.Graph v5+
                    var addPasswordRequestBody = new Microsoft.Graph.Applications.Item.AddPassword.AddPasswordPostRequestBody
                    {
                        PasswordCredential = passwordCredential
                    };

                    var addPasswordResult = await _graphClient.Applications[createdApp.Id]
                        .AddPassword
                        .PostAsync(addPasswordRequestBody);

                    secretText = addPasswordResult.SecretText;
                    secretEnd = addPasswordResult.EndDateTime;

                    _logger.LogInformation("Client secret generated.");

                    // 3. Store in CyberArk Secrets Hub
                    keyVaultSecretUri = await StoreSecretInKeyVaultAsync(createdApp.DisplayName, secretText, createdApp.AppId, secretEnd);
                }

                // 4. Create Service Principal (Enterprise App) in the local tenant
                // This is often required for the app to be usable
                var servicePrincipal = new ServicePrincipal
                {
                    AppId = createdApp.AppId
                };
                await _graphClient.ServicePrincipals.PostAsync(servicePrincipal);
                _logger.LogInformation("Service Principal created.");

                var result = new AppRegistrationResult
                {
                    AppId = createdApp.AppId,
                    ObjectId = createdApp.Id,
                    DisplayName = createdApp.DisplayName,
                    // ClientSecret is forcefully made null to ensure it is never returned in plain text as per requirement
                    ClientSecret = null, 
                    SecretExpiration = secretEnd,
                    Status = "Provisioned",
                    KeyVaultReference = keyVaultSecretUri
                };

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning application");
                throw;
            }
        }

        private async Task<string> StoreSecretInKeyVaultAsync(string appDisplayName, string secretValue, string appId, DateTimeOffset? expiration)
        {
            try
            {
                // Retrieve CyberArk configuration
                var cyberArkUrl = _configuration["CyberArk:Url"];
                var cyberArkAppId = _configuration["CyberArk:AppId"];
                var safeName = _configuration["CyberArk:SafeName"];

                if (string.IsNullOrEmpty(cyberArkUrl) || string.IsNullOrEmpty(safeName))
                {
                    _logger.LogWarning("CyberArk configuration is missing. Secret cannot be synchronized.");
                    return "CyberArk not configured";
                }

                _logger.LogInformation($"Synchronizing secret for {appDisplayName} with CyberArk Secrets Hub at {cyberArkUrl}");

                // Construct the payload for CyberArk Secrets Hub synchronization
                // Note: The actual payload structure depends on the specific CyberArk API version being used.
                // This is a generic representation for synchronizing a secret.
                var payload = new
                {
                    name = $"AppSecret-{appId.Substring(0, 8)}",
                    value = secretValue,
                    safeName = safeName,
                    properties = new
                    {
                        AppId = appId,
                        AppName = appDisplayName,
                        ExpirationDate = expiration
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Assuming authentication is handled via a client certificate or token configured in the HttpClient or headers
                // For this implementation, we'll assume the _httpClient is pre-configured or we add a specific header if needed.
                // In a real scenario, you might need to fetch a token first.
                
                // Example: Adding an API key or Token if available in config
                if (!string.IsNullOrEmpty(_configuration["CyberArk:ApiKey"]))
                {
                    _httpClient.DefaultRequestHeaders.Remove("Authorization");
                    _httpClient.DefaultRequestHeaders.Add("Authorization", _configuration["CyberArk:ApiKey"]);
                }

                var response = await _httpClient.PostAsync($"{cyberArkUrl}/api/secrets", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Secret successfully synchronized with CyberArk Secrets Hub.");
                    return "Synchronized with CyberArk";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to synchronize secret with CyberArk. Status: {response.StatusCode}, Error: {errorContent}");
                    return $"Error synchronizing secret: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to synchronize secret with CyberArk for {appDisplayName}");
                return $"Error synchronizing secret: {ex.Message}";
            }
        }
    }
}
