param name string
param location string
param tags object = {}
param logAnalyticsWorkspaceId string

@description('Serialized JSON that defines the workbook layout and queries.')
param workbookContent string

resource workbook 'Microsoft.Insights/workbooks@2023-06-01' = {
  name: guid(resourceGroup().id, name)
  location: location
  tags: tags
  kind: 'shared'
  properties: {
    displayName: name
    category: 'workbook'
    sourceId: logAnalyticsWorkspaceId
    serializedData: workbookContent
  }
}

output id string = workbook.id
output name string = workbook.name
