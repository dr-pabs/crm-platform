// infra/modules/containerAppsEnvironment.bicep
// Azure Container Apps Environment — shared managed environment for all services.

@description('Location for all resources')
param location string

@description('Environment name (e.g. crm-dev-cae)')
param environmentName string

@description('Log Analytics workspace ID for OTel / App Insights integration')
param logAnalyticsWorkspaceId string

@description('Log Analytics workspace shared key')
@secure()
param logAnalyticsWorkspaceKey string

@description('Whether to use a dedicated workload profile (prod) or consumption-only (non-prod)')
param useWorkloadProfile bool = false

@description('Tags')
param tags object = {}

resource containerAppsEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: environmentName
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceId
        sharedKey: logAnalyticsWorkspaceKey
      }
    }
    workloadProfiles: useWorkloadProfile ? [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
      {
        name: 'general'
        workloadProfileType: 'D4'
        minimumCount: 1
        maximumCount: 10
      }
    ] : [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

@description('Resource ID of the Container Apps Environment')
output environmentId string = containerAppsEnv.id

@description('Default domain for the environment')
output defaultDomain string = containerAppsEnv.properties.defaultDomain

@description('Resource name')
output name string = containerAppsEnv.name
