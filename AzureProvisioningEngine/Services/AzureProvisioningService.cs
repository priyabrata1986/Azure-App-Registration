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
                    ClientSecret = secretText, // Be careful returning this in real API, usually only shown once
                    SecretExpiration = secretEnd,
                    Status = "Provisioned"
                };

                // 3. Sync to CyberArk / Secrets Hub
                if (request.GenerateClientSecret)
                {
                    await SyncToSecretsHubAsync(result);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error provisioning application");
                throw;
            }
        }

        private async Task SyncToSecretsHubAsync(AppRegistrationResult result)
        {
            try 
            {
                _logger.LogInformation($"Starting synchronization with Secrets Hub for App: {result.DisplayName} ({result.AppId})");

                var secretsHubUrl = _configuration["SecretsHub:ApiUrl"];
                var apiKey = _configuration["SecretsHub:ApiKey"];
                var safeName = _configuration["SecretsHub:SafeName"] ?? "Azure_App_Secrets";

                if (string.IsNullOrEmpty(secretsHubUrl) || string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogWarning("Secrets Hub configuration missing (Url or ApiKey). Skipping sync.");
                    return;
                }

                var payload = new
                {
                    Safe = safeName,
                    Object = result.DisplayName,
                    Secret = result.ClientSecret,
                    Properties = new 
                    {
                        AppId = result.AppId,
                        ObjectId = result.ObjectId,
                        Expiration = result.SecretExpiration
                    }
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

                var response = await _httpClient.PostAsync($"{secretsHubUrl}/secrets", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"[SUCCESS] Credentials successfully synced to Safe: '{safeName}'. Reference ID: {result.ObjectId}");
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to sync to Secrets Hub. Status: {response.StatusCode}, Details: {errorContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to sync credentials to Secrets Hub for {result.DisplayName}");
                // We log the error but do not re-throw to ensure the provisioning result is returned to the user
            }
        }
    }
}
