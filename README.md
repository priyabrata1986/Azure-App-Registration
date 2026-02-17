# Azure Provisioning Engine - API Usage Guide

## Overview
This application is a backend **REST API** service designed to automate Azure App Registrations. It does **not** have a front-end user interface (web page). Accessing the root URL (`https://localhost:7014/`) directly in a browser will result in a `404 Not Found` error because no route is configured for the root path.

## How to Access and Test

To interact with this service, you must use an API testing tool like **Postman**, **Insomnia**, or **curl** to send HTTP requests to the exposed endpoints.

### 1. Endpoint Details

*   **URL**: `https://localhost:7014/api/provisioning/register`
*   **Method**: `POST`
*   **Content-Type**: `application/json`

### 2. Sample Request

**Body (JSON):**

```json
{
  "appName": "MyNewApp_001",
  "ownerEmail": "admin@example.com",
  "businessJustification": "Project X Automation",
  "signInAudience": "AzureADMyOrg",
  "redirectUris": [
    "https://myapp.example.com/signin-oidc"
  ],
  "description": "App for testing automation",
  "generateClientSecret": true
}
```

### 3. Expected Response

**Success (200 OK):**

```json
{
  "appId": "00000000-0000-0000-0000-000000000000",
  "objectId": "11111111-1111-1111-1111-111111111111",
  "displayName": "MyNewApp_001",
  "clientSecret": "secret-value-here...",
  "secretExpiration": "2024-12-31T23:59:59+00:00",
  "status": "Provisioned"
}
```

### 4. Notifications

Upon a successful request:
1.  **Request Initiated Email**: Sent to `ownerEmail` immediately.
2.  **Provisioning Completed Email**: Sent to `ownerEmail` after the app is created in Azure and secrets are synced.

### 5. Troubleshooting

*   **404 Not Found**: Ensure you are using the full path `/api/provisioning/register` and not just the root URL.
*   **400 Bad Request**: Check that your JSON body is valid and includes all required fields (`appName`, `ownerEmail`, `businessJustification`).
*   **500 Internal Server Error**: Check the application logs. This usually indicates an issue with Azure credentials or the Secrets Hub connection.
