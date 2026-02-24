# ZavaStorefront Infrastructure

This directory contains the Azure infrastructure definitions for the ZavaStorefront application, managed with [Azure Developer CLI (AZD)](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/) and [Bicep](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/).

## Resources Provisioned

All resources are deployed together in a single resource group in the **westus3** region.

| Resource | Purpose | SKU |
|---|---|---|
| Azure Container Registry (ACR) | Stores Docker images; no admin credentials, RBAC only | Basic |
| Azure App Service Plan | Hosts the Linux Web App | B1 (Basic) |
| Azure App Service (Web App for Containers) | Runs the ZavaStorefront Docker container | - |
| Log Analytics Workspace | Backend for Application Insights telemetry | PerGB2018 |
| Application Insights | Application performance monitoring | Workspace-based |
| Microsoft AI Foundry (Azure AI Services) | GPT-4 and Phi model access | S0 |

## Architecture Decisions

- **No local Docker required**: Image builds and pushes use `az acr build` (cloud-side builds) or GitHub Actions hosted runners.
- **Azure RBAC for image pulls**: The App Service uses a system-assigned managed identity with the `AcrPull` role on the Container Registry. Admin credentials are disabled.
- **Workspace-based Application Insights**: Linked to a Log Analytics Workspace for full observability features.
- **Microsoft AI Foundry in westus3**: GPT-4 and Phi-3.5-mini-instruct are available in this region.

## File Structure

```
infra/
├── main.bicep              # Root orchestration template (subscription scope)
├── main.parameters.json    # AZD parameter file
├── README.md               # This file
└── modules/
    ├── acr.bicep           # Azure Container Registry
    ├── appInsights.bicep   # Application Insights
    ├── appService.bicep    # App Service Plan + Web App + AcrPull role assignment
    ├── aiFoundry.bicep     # Microsoft AI Foundry with GPT-4 and Phi deployments
    └── logAnalytics.bicep  # Log Analytics Workspace
```

## Deployment

### Prerequisites

- [Azure Developer CLI](https://learn.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd) installed
- An active Azure subscription
- Sufficient quota for Microsoft AI Foundry in westus3

### Deploy

```bash
# From the repository root:

# Initialize AZD (first time only)
azd init

# Preview what will be deployed
azd provision --preview

# Provision infrastructure and deploy the application
azd up
```

### Build and push Docker image (without local Docker)

Use Azure Container Registry Tasks to build in the cloud:

```bash
az acr build \
  --registry <your-acr-name> \
  --image zava-storefront:latest \
  --file src/Dockerfile \
  ./src
```

## Cost Notes

This configuration uses minimal-cost SKUs appropriate for a dev environment:

- **ACR Basic**: ~$0.167/day
- **App Service B1**: ~$0.018/hour
- **Log Analytics**: Pay-per-GB ingested (first 5 GB/month free)
- **AI Foundry S0**: Pay-per-use for model API calls
