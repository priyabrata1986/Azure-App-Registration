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

                    // 3. Store in Azure Key Vault
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
                    // ClientSecret is intentionally suppressed from response for security
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
                var keyVaultUrl = _configuration["KeyVault:Url"];
                if (string.IsNullOrEmpty(keyVaultUrl))
                {
                    _logger.LogWarning("Key Vault URL is not configured. Secret cannot be stored.");
                    return "Key Vault not configured";
                }

                _logger.LogInformation($"Storing secret for {appDisplayName} in Key Vault: {keyVaultUrl}");

                var credential = new DefaultAzureCredential();
                var client = new SecretClient(new Uri(keyVaultUrl), credential);

                // Create a valid secret name (alphanumeric and dashes only)
                // We'll use the AppDisplayName but sanitize it, or fallback to AppId if name is too complex
                // For simplicity, let's use a prefix + sanitized name
                var sanitizedName = System.Text.RegularExpressions.Regex.Replace(appDisplayName, "[^a-zA-Z0-9-]", "-");
                var secretName = $"AppSecret-{sanitizedName}-{appId.Substring(0, 4)}"; 

                // Ensure secret name doesn't end with a dash and is within length limits if necessary
                secretName = secretName.Trim('-');

                var secret = new KeyVaultSecret(secretName, secretValue);
                secret.Properties.ExpiresOn = expiration;
                secret.Properties.ContentType = $"Client Secret for AppId: {appId}";

                KeyVaultSecret createdSecret = await client.SetSecretAsync(secret);
                
                _logger.LogInformation($"Secret stored successfully. ID: {createdSecret.Id}");
                return createdSecret.Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to store secret in Key Vault for {appDisplayName}");
                // We log but don't throw to ensure the main provisioning flow completes, 
                // though in a real strict env you might want to fail the whole process or rollback.
                return $"Error storing secret: {ex.Message}";
            }
        }
    }
}
