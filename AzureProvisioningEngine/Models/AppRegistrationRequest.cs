using System;
using System.Collections.Generic;

namespace AzureProvisioningEngine.Models
{
    public class AppRegistrationRequest
    {
        public string AppName { get; set; }
        public string OwnerEmail { get; set; }
        public string Description { get; set; }
        public string SignInAudience { get; set; } // AzureADMyOrg, AzureADMultipleOrgs
        public string[] RedirectUris { get; set; }
        public bool GenerateClientSecret { get; set; }
        public string BusinessJustification { get; set; }
    }

    public class AppRegistrationResult
    {
        public string AppId { get; set; }
        public string ObjectId { get; set; }
        public string DisplayName { get; set; }
        public string ClientSecret { get; set; } // Only returned if generated
        public DateTimeOffset? SecretExpiration { get; set; }
        public string Status { get; set; }
    }
}
