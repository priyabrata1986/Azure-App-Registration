# Azure Provisioning Engine

This project provides a service for provisioning Azure AD Applications and storing their secrets securely in CyberArk.

## Key Features

*   **App Registration**: Automates the creation of Azure AD Applications.
*   **Secret Management**: Generates client secrets and securely stores them in CyberArk Secrets Hub.
*   **Service Principal Creation**: Automatically creates a Service Principal for the registered application.

## Development Setup

### Required Tools

To build and run this application locally, you need the following tools installed on your machine:

1.  **source code editor**: Visual Studio 2022 or Visual Studio Code.
2.  **.NET SDK**: .NET 9.0 SDK or later. You can download it from [dotnet.microsoft.com](https://dotnet.microsoft.com/download).
3.  **Git**: For cloning the repository.

### Building the Application

1.  **Clone the repository**:
    ```bash
    git clone <repository-url>
    cd AzureKrishna
    ```

2.  **Restore dependencies**:
    Navigate to the project folder and run:
    ```bash
    dotnet restore
    ```

3.  **Build the project**:
    ```bash
    dotnet build
    ```

### Running the Application

1.  **Configure Settings**:
    Ensure your `appsettings.json` is configured with the necessary Azure and CyberArk settings as described in the [Configuration](#configuration) section below.

2.  **Run the application**:
    ```bash
    dotnet run --project AzureProvisioningEngine
    ```
    
    *Note: Ensure you are running the command from the solution root or adjust the path to the project file accordingly.*

## API Documentation & Testing

The application exposes a REST API documented using Swagger (OpenAPI). You can use the Swagger UI to interactively test the API endpoints.

### Accessing Swagger UI

Once the application is running, open your web browser and navigate to the following URL:

*   **https://localhost:7014/index.html** (or simply **https://localhost:7014/** as it is served at the root)

*Note: The port `7014` is the default HTTPS port configured in `launchSettings.json`. If you are using the HTTP profile, the URL will be `http://localhost:5200`.*

### How to Test using Swagger

1.  **Navigate to the URL**: Open the link above in your browser. You should see the "Azure Provisioning Engine API" documentation page.
2.  **Select an Endpoint**: Click on the `POST /api/provisioning` (or similar) endpoint to expand its details.
3.  **Try it out**: Click the **"Try it out"** button on the right side of the endpoint description.
4.  **Enter Request Body**: In the "Request body" text area, paste a sample JSON payload. You can use the example provided in the [Usage Examples](#usage-examples) section below.
    *   *Example Payload:*
        ```json
        {
          "appName": "TestApp-Swagger",
          "ownerEmail": "admin@example.com",
          "businessJustification": "Testing Swagger Integration",
          "signInAudience": "AzureADMyOrg",
          "generateClientSecret": true
        }
        ```
5.  **Execute**: Click the big blue **"Execute"** button.
6.  **View Response**: Scroll down to the "Responses" section to see the API response code (e.g., 200 Success) and the response body containing the `AppRegistrationResult`.

## Prerequisites for Testing

Before running or testing the application, ensure the following prerequisites are met:

1.  **Azure Subscription**: You must have an active Azure subscription with permissions to register applications in Azure Active Directory (Entra ID).
2.  **CyberArk Access**: Access to a CyberArk Secrets Hub instance is required. You need the URL, App ID, and a target Safe name.
3.  **Configuration**: The `appsettings.json` file must be configured with valid CyberArk details (Url, AppId, SafeName) and Azure AD credentials (if not using Managed Identity).
4.  **Network Connectivity**: The host running the application must have outbound network connectivity to:
    *   `graph.microsoft.com` (for Azure AD operations)
    *   The configured CyberArk Secrets Hub URL.
5.  **Permissions**: The identity running the application (User or Service Principal) needs the `Application.ReadWrite.All` permission in Microsoft Graph to create and manage applications.

## Configuration

To enable CyberArk Secrets Hub integration, ensure the following configuration is present in your `appsettings.json` or environment variables:

```json
{
  "CyberArk": {
    "Url": "https://your-cyberark-instance.com",
    "AppId": "your-cyberark-app-id",
    "SafeName": "your-target-safe-name",
    "ApiKey": "your-api-key-if-required"
  }
}
```

*   **Url**: The base URL of your CyberArk Secrets Hub instance.
*   **AppId**: The application identifier within CyberArk.
*   **SafeName**: The name of the safe where secrets should be stored.
*   **ApiKey**: (Optional) API Key for authentication if required by your specific CyberArk setup.

## Usage Examples

### 1. Provisioning an Application

**Input (`AppRegistrationRequest`):**

```json
{
  "AppName": "MyNewApp",
  "OwnerEmail": "owner@example.com",
  "BusinessJustification": "Project X Requirement",
  "SignInAudience": "AzureADMyOrg",
  "RedirectUris": [
    "https://myapp.example.com/signin-oidc"
  ],
  "Description": "App for Project X",
  "GenerateClientSecret": true
}
```

**Output (`AppRegistrationResult`):**

```json
{
  "AppId": "00000000-0000-0000-0000-000000000000",
  "ObjectId": "11111111-1111-1111-1111-111111111111",\n  "DisplayName": "MyNewApp",
  "ClientSecret": "", 
  "SecretExpiration": "2024-12-31T23:59:59+00:00",
  "Status": "Provisioned",
  "KeyVaultReference": "Synchronized with CyberArk"
}
```

### 2. Provisioning without Secret Generation

**Input:** Set `"GenerateClientSecret": false`.

**Output:**
*   `ClientSecret` will be `"No Secret Generated"`.
*   `KeyVaultReference` will be null or empty.

## Feature Details

### CyberArk Secrets Hub Integration

When `GenerateClientSecret` is set to `true`, the service performs the following:
1.  Generates a new client secret for the Azure AD Application.
2.  Immediately sends this secret to the configured CyberArk Secrets Hub.
3.  The secret is stored in the specified `SafeName` with a name format `AppSecret-{AppIdPrefix}`.
4.  **Security Note**: The raw secret is **never** returned in the API response to the caller. It is only transmitted to CyberArk.

### Client Secret Handling

The `ClientSecret` property in the response object adheres to strict security and stability guidelines:

*   **Non-Null Guarantee**: The `ClientSecret` property is initialized to `string.Empty` by default. It will **never** be `null`.
*   **Values**:
    *   `""` (Empty String): Indicates a secret was generated and offloaded to CyberArk, but is redacted from the response for security.
    *   `"No Secret Generated"`: Indicates that secret generation was skipped (e.g., `GenerateClientSecret` was false).
*   **Developer Guidance**: Always check `string.IsNullOrEmpty(ClientSecret)` to determine if a usable secret value is present (which should generally be false in this secure implementation).

## Data Models

### AppRegistrationResult

The `AppRegistrationResult` class represents the outcome of an application provisioning request.

#### ClientSecret Property

The `ClientSecret` property holds the clear-text value of the generated client secret for the application.

*   **Non-Null Guarantee**: The `ClientSecret` property is initialized to `string.Empty` by default. This ensures that the property is never `null`, even if no secret was generated or if the secret generation failed.
*   **Value**:
    *   If a secret is successfully generated, this property contains the secret text.
    *   If no secret is generated (e.g., `GenerateClientSecret` was set to `false`), this property will contain the string `"No Secret Generated"`.
    *   In the unlikely event of a manual override setting it to `null`, consumers should handle it gracefully, but the design enforces a non-null string to prevent `NullReferenceException` in consuming layers.

**Implications of a Null State (Theoretical):**
Although the current implementation enforces a non-null value, if `ClientSecret` were to be manually set to `null` via reflection or other means:
1.  **Serialization**: JSON serialization might omit the field or serialize it as `null`, depending on settings.
2.  **Consuming Logic**: Any code expecting a string operation (like `.Length` or `.Substring`) on this property would throw a `NullReferenceException`.
3.  **Security**: A `null` value implies no secret exists, which is distinct from an empty string (which might imply a failed generation or a specific "no secret" state).

**Recommendation:** Always check `string.IsNullOrEmpty(ClientSecret)` or `string.IsNullOrWhiteSpace(ClientSecret)` before using the value.
