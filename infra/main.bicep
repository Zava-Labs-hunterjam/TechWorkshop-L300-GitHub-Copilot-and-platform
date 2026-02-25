targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment, used to generate unique resource names.')
param environmentName string

@minLength(1)
@description('Primary location for all resources.')
param location string = 'westus3'

var tags = { 'azd-env-name': environmentName }
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))

// Resource Group
resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

// Log Analytics Workspace (required by Application Insights workspace-based mode)
module logAnalytics './modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  scope: rg
  params: {
    name: 'log-${resourceToken}'
    location: location
    tags: tags
  }
}

// Application Insights
module appInsights './modules/appInsights.bicep' = {
  name: 'appInsights'
  scope: rg
  params: {
    name: 'appi-${resourceToken}'
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

// Azure Container Registry
module acr './modules/acr.bicep' = {
  name: 'acr'
  scope: rg
  params: {
    name: 'cr${resourceToken}'
    location: location
    tags: tags
  }
}

// Linux App Service Plan + Web App for Containers + AcrPull role assignment
module appService './modules/appService.bicep' = {
  name: 'appService'
  scope: rg
  params: {
    appName: 'app-${resourceToken}'
    planName: 'plan-${resourceToken}'
    location: location
    tags: tags
    acrLoginServer: acr.outputs.loginServer
    acrName: acr.outputs.name
    appInsightsConnectionString: appInsights.outputs.connectionString
    appInsightsInstrumentationKey: appInsights.outputs.instrumentationKey
    aiServicesName: aiFoundry.outputs.name
    aiServicesEndpoint: aiFoundry.outputs.endpoint
  }
  dependsOn: [aiFoundry]
}

// Microsoft AI Foundry (Azure AI Services) with GPT-4o and Phi models
module aiFoundry './modules/aiFoundry.bicep' = {
  name: 'aiFoundry'
  scope: rg
  params: {
    name: 'aif-${resourceToken}'
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

// Azure Workbook – AI Services Observability
module workbook './modules/workbook.bicep' = {
  name: 'workbook'
  scope: rg
  params: {
    name: 'AI Services Observability'
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
    workbookContent: loadTextContent('./modules/workbook.json')
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = acr.outputs.loginServer
output AZURE_CONTAINER_REGISTRY_NAME string = acr.outputs.name
output APP_SERVICE_NAME string = appService.outputs.name
output APP_SERVICE_URI string = appService.outputs.uri
output APPLICATIONINSIGHTS_CONNECTION_STRING string = appInsights.outputs.connectionString
output AI_FOUNDRY_ENDPOINT string = aiFoundry.outputs.endpoint
