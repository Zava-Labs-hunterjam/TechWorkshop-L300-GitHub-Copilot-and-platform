# CI/CD Pipeline Setup

This workflow builds the .NET app as a Docker container, pushes it to Azure Container Registry, and deploys it to App Service. It uses OIDC (federated credentials) for passwordless authentication.

## Prerequisites

### 1. Create a Service Principal

```bash
az ad sp create-for-rbac --name "gh-deploy" --role Contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/<RESOURCE_GROUP>
```

Note the `appId` and `tenantId` from the output.

### 2. Add Federated Credential

In the Azure Portal or CLI, add a federated credential to the service principal:

```bash
az ad app federated-credential create --id <APP_OBJECT_ID> --parameters '{
  "name": "github-actions-main",
  "issuer": "https://token.actions.githubusercontent.com",
  "subject": "repo:<GITHUB_ORG>/<REPO_NAME>:ref:refs/heads/main",
  "audiences": ["api://AzureADTokenExchange"]
}'
```

## GitHub Secrets

Create the following secrets in **Settings > Secrets and variables > Actions > Secrets**:

| Secret | Description |
|---|---|
| `AZURE_CLIENT_ID` | Service principal Application (client) ID |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

## GitHub Variables

Create the following variables in **Settings > Secrets and variables > Actions > Variables**:

| Variable | Description | Example |
|---|---|---|
| `ACR_NAME` | Azure Container Registry name | `cr<resourceToken>` |
| `ACR_LOGIN_SERVER` | ACR login server URL | `cr<resourceToken>.azurecr.io` |
| `APP_SERVICE_NAME` | App Service name | `app-<resourceToken>` |

You can find the actual values by running:

```bash
azd env get-values
```

The relevant outputs are `AZURE_CONTAINER_REGISTRY_NAME`, `AZURE_CONTAINER_REGISTRY_ENDPOINT`, and `APP_SERVICE_NAME`.
