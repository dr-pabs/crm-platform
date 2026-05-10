// infra/modules/containerRegistry.bicep
// Azure Container Registry — stores all service container images.

@description('Location for all resources')
param location string

@description('ACR name (globally unique, alphanumeric only)')
param acrName string

@description('SKU — Basic for dev, Premium for prod (geo-replication, private endpoint)')
@allowed(['Basic', 'Standard', 'Premium'])
param sku string = 'Standard'

@description('Whether to enable the admin user (needed for some local dev scenarios; disable in prod)')
param adminUserEnabled bool = false

@description('Tags')
param tags object = {}

resource acr 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  properties: {
    adminUserEnabled: adminUserEnabled
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      trustPolicy: {
        type: 'Notary'
        status: 'disabled'
      }
      retentionPolicy: {
        days: sku == 'Premium' ? 30 : 7
        status: 'enabled'
      }
    }
    publicNetworkAccess: 'Enabled'
    zoneRedundancy: sku == 'Premium' ? 'Enabled' : 'Disabled'
    networkRuleBypassOptions: 'AzureServices'
  }
}

@description('Fully qualified login server (e.g. myacr.azurecr.io)')
output loginServer string = acr.properties.loginServer

@description('Resource ID — used to assign AcrPull role to Container App managed identities')
output resourceId string = acr.id

@description('Resource name')
output name string = acr.name
