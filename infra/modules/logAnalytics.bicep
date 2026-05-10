// infra/modules/logAnalytics.bicep
// Log Analytics workspace — backing store for Container Apps logs and App Insights.

@description('Location for all resources')
param location string

@description('Workspace name')
param workspaceName string

@description('Retention in days (30 for dev, 90 for prod)')
param retentionDays int = 30

@description('Tags')
param tags object = {}

resource workspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

@description('Workspace ID (customerId) — used by Container Apps environment')
output workspaceId string = workspace.properties.customerId

@description('Shared key — used by Container Apps environment')
output sharedKey string = workspace.listKeys().primarySharedKey

@description('Resource ID')
output resourceId string = workspace.id
