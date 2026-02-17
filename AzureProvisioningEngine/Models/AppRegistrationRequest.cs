using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace AzureProvisioningEngine.Models
{
    public class AppRegistrationRequest
    {
        [Required]
        public string AppName { get; set; }

        [Required]
        [EmailAddress]
        public string OwnerEmail { get; set; }

        [Required]
        public string BusinessJustification { get; set; }

        public string SignInAudience { get; set; } = "AzureADMyOrg"; // Default to Single Tenant

        public List<string> RedirectUris { get; set; }

        public string Description { get; set; }

        public bool GenerateClientSecret { get; set; } = true;
    }

    public class AppRegistrationResult
    {
        public string AppId { get; set; }
        public string ObjectId { get; set; }
        public string DisplayName { get; set; }
        
        // Secret is no longer returned in plain text
        // Initialized to empty string to ensure it is never null as per requirement
        public string ClientSecret { get; set; } = string.Empty;
        
        public System.DateTimeOffset? SecretExpiration { get; set; }
        public string Status { get; set; }
        
        // Reference to the secret in Key Vault
        public string KeyVaultReference { get; set; }
    }
}
