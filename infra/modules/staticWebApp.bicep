// infra/modules/staticWebApp.bicep
// Azure Static Web Apps — staff portal and customer portal.
// ADR 0007: React SPA served from SWA; API calls routed through APIM.

@description('Azure region (SWA limited regions — eastus2 recommended)')
param location string

@description('Static Web App name')
param staticWebAppName string

@description('SKU — Free for dev preview, Standard for prod (custom domain, private endpoints)')
@allowed(['Free', 'Standard'])
param sku string = 'Standard'

@description('Optional custom domain to link')
param customDomain string = ''

@description('Tags')
param tags object = {}

resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
    enterpriseGradeCdnStatus: sku == 'Standard' ? 'Enabled' : 'Disabled'
  }
}

resource customDomainResource 'Microsoft.Web/staticSites/customDomains@2023-01-01' = if (!empty(customDomain)) {
  parent: staticWebApp
  name: customDomain
  properties: {}
}

output staticWebAppId string = staticWebApp.id
output defaultHostname string = staticWebApp.properties.defaultHostname
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey

@description('Location for all resources')
param location string = resourceGroup().location

@description('Resource name')
param name string

// TODO: add parameters, resources, and outputs
