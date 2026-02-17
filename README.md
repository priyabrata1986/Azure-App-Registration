# Azure Provisioning Engine

This project provides an engine for provisioning Azure resources, specifically focusing on App Registrations in Microsoft Entra ID (formerly Azure AD).

## Features

*   **App Registration:** Automates the creation of App Registrations.
*   **Client Secret Management:** Generates client secrets and securely stores them in Azure Key Vault.
*   **Service Principal Creation:** Automatically creates the corresponding Service Principal (Enterprise App) in the local tenant.

## Configuration

The application requires configuration in `appsettings.json` (or `appsettings.Development.json` for local development).

### Azure Key Vault Integration

Generated client secrets are **not** returned in the API response. Instead, they are securely stored in an Azure Key Vault.

**Required Configuration:**

You must specify the URL of the Azure Key Vault where secrets should be stored.

```json
"KeyVault": {
  "Url": "https://<your-key-vault-name>.vault.azure.net/"
}
```

**Permissions:**

The identity running this application (e.g., Visual Studio User, Managed Identity, or Service Principal) must have the following permissions on the target Key Vault:
*   **Secret Management:** `Set` (to create/update secrets).

### Secrets Hub Role

The previous integration with "Secrets Hub" for storing client secrets has been deprecated in favor of Azure Key Vault. The application no longer synchronizes secrets to the Secrets Hub.

## API Usage

### 1. Provisioning an Application

**Endpoint:** `POST /api/provision` (Example endpoint)

**Request Body (Input Sample):**

```json
{
  "AppName": "MyNewApp",
  "OwnerEmail": "admin@example.com",
  "BusinessJustification": "Project X Requirement",
  "SignInAudience": "AzureADMyOrg",
  "RedirectUris": [
    "https://myapp.example.com/signin-oidc"
  ],
  "Description": "App for Project X",
  "GenerateClientSecret": true
}
```

**Response (Output Sample):**

Note that the `ClientSecret` is intentionally null. The secret has been stored in Azure Key Vault, and the `KeyVaultReference` provides the Secret ID.

```json
{
  "appId": "00000000-0000-0000-0000-000000000000",
  "objectId": "11111111-1111-1111-1111-111111111111",
  "displayName": "MyNewApp",
  "clientSecret": null,
  "secretExpiration": "2024-12-31T23:59:59.999Z",
  "status": "Provisioned",
  "keyVaultReference": "https://<your-key-vault-name>.vault.azure.net/secrets/AppSecret-MyNewApp-0000/22222222222222222222222222222222"
}
```

## Getting Started

1.  Clone the repository.
2.  Update `appsettings.json` with your Azure Key Vault URL.
3.  Ensure your environment has the necessary Azure credentials (e.g., via `az login` or Visual Studio).
4.  Run the application.
