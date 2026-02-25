param appName string
param planName string
param location string
param tags object = {}
param acrLoginServer string
param acrName string
param appInsightsConnectionString string
param appInsightsInstrumentationKey string
param aiServicesName string
param aiServicesEndpoint string

// AcrPull built-in role definition ID
var acrPullRoleDefinitionId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  '7f951dda-4ed3-4680-a7ca-43fe172d538d'
)

// Cognitive Services User built-in role definition ID
var cognitiveServicesUserRoleId = subscriptionResourceId(
  'Microsoft.Authorization/roleDefinitions',
  'a97b65f3-24c7-4388-baec-2e87135dc908'
)

resource appServicePlan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: planName
  location: location
  tags: tags
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource appService 'Microsoft.Web/sites@2022-09-01' = {
  name: appName
  location: location
  tags: union(tags, { 'azd-service-name': 'src' })
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/zava-storefront:latest'
      acrUseManagedIdentityCreds: true
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'WEBSITES_PORT'
          value: '8080'
        }
        {
          name: 'AiFoundry__Endpoint'
          value: aiServicesEndpoint
        }
      ]
    }
  }
}

// Reference the existing ACR to scope the role assignment
resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: acrName
}

// Grant the Web App's managed identity the AcrPull role on the Container Registry
resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(acr.id, appService.id, acrPullRoleDefinitionId)
  scope: acr
  properties: {
    roleDefinitionId: acrPullRoleDefinitionId
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Reference the existing AI Services account to scope the role assignment
resource aiServices 'Microsoft.CognitiveServices/accounts@2024-04-01-preview' existing = {
  name: aiServicesName
}

// Grant the Web App's managed identity the Cognitive Services User role
resource cognitiveServicesRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(aiServices.id, appService.id, cognitiveServicesUserRoleId)
  scope: aiServices
  properties: {
    roleDefinitionId: cognitiveServicesUserRoleId
    principalId: appService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output id string = appService.id
output name string = appService.name
output uri string = 'https://${appService.properties.defaultHostName}'
output principalId string = appService.identity.principalId
