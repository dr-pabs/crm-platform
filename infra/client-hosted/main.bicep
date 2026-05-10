// infra/client-hosted/main.bicep
// Client-hosted deployment — provisions a dedicated single-tenant CRM environment
// for clients who require data residency or isolated infrastructure.
//
// Deploy via:
//   az deployment sub create \
//     --location <region> \
//     --template-file infra/client-hosted/main.bicep \
//     --parameters @infra/client-hosted/parameters/<client-id>.bicepparam
//
// See parameters/client-template.bicepparam for all required values.

targetScope = 'subscription'

@description('Deployment environment (always "prod" for client-hosted)')
@allowed(['prod', 'staging'])
param environment string = 'prod'

@description('Azure region for all resources')
param location string = 'uksouth'

@description('Resource group name — unique per client deployment')
param resourceGroupName string

@description('Container image tag (must match a scanned image from the platform ACR)')
param imageTag string

@description('Publisher email for APIM')
param publisherEmail string

@description('Publisher organisation name')
param publisherName string = 'CRM Platform'

@description('Entra ID tenant ID for JWT validation (platform Entra tenant)')
param entraTenantId string

@description('Entra External ID audience for this client tenant')
param entraAudience string

@description('Object ID of the deployment principal (for SQL AAD admin)')
param deploymentPrincipalObjectId string

@description('Client identifier — short, lowercase, alphanumeric only (used in resource names)')
@minLength(3)
@maxLength(12)
param clientId string

// ─── Resource name helpers ────────────────────────────────────────────────────
var prefix           = 'crm-${clientId}'
var acrName          = replace('${prefix}acr', '-', '')
var logAnalyticsName = '${prefix}-logs'
var containerEnvName = '${prefix}-cae'
var sqlServerName    = '${prefix}-sql'
var sbNamespaceName  = '${prefix}-sb'
var keyVaultName     = '${prefix}-kv'
var storageAcctName  = replace('${prefix}sa', '-', '')
var appConfigName    = '${prefix}-appconfig'
var apimName         = '${prefix}-apim'
var staffPortalName  = '${prefix}-staff-portal'
var customerPortalName = '${prefix}-customer-portal'

// ─── Resource Group ───────────────────────────────────────────────────────────
resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: resourceGroupName
  location: location
  tags: {
    environment: environment
    project: 'crm-platform'
    client: clientId
    managedBy: 'bicep'
  }
}

// ─── Observability ────────────────────────────────────────────────────────────
module logAnalytics '../modules/logAnalytics.bicep' = {
  name: 'logAnalytics'
  scope: rg
  params: {
    location: location
    workspaceName: logAnalyticsName
    retentionDays: 90
    tags: rg.tags
  }
}

// ─── Container Registry (client-dedicated for data-residency compliance) ──────
module acr '../modules/containerRegistry.bicep' = {
  name: 'containerRegistry'
  scope: rg
  params: {
    location: location
    acrName: acrName
    sku: 'Premium'
    adminUserEnabled: false
    tags: rg.tags
  }
}

// ─── Key Vault ────────────────────────────────────────────────────────────────
module keyVault '../modules/keyVault.bicep' = {
  name: 'keyVault'
  scope: rg
  params: {
    location: location
    keyVaultName: keyVaultName
    tags: rg.tags
    secretsUserPrincipalIds: []
  }
}

// ─── Storage Account ──────────────────────────────────────────────────────────
module storage '../modules/storageAccount.bicep' = {
  name: 'storage'
  scope: rg
  params: {
    location: location
    storageAccountName: storageAcctName
    sku: 'Standard_ZRS'
    blobContainers: ['attachments', 'durable-functions']
    tags: rg.tags
  }
}

// ─── Service Bus ──────────────────────────────────────────────────────────────
module serviceBus '../modules/serviceBus.bicep' = {
  name: 'serviceBus'
  scope: rg
  params: {
    location: location
    namespaceName: sbNamespaceName
    capacity: 2
    tags: rg.tags
  }
}

// ─── SQL Database ─────────────────────────────────────────────────────────────
module sql '../modules/sqlDatabase.bicep' = {
  name: 'sqlDatabase'
  scope: rg
  params: {
    location: location
    serverName: sqlServerName
    databaseName: 'CrmPlatform'
    skuTier: 'Hyperscale'
    vCores: 4
    provisionAnalyticsReplica: true
    sqlAdminObjectId: deploymentPrincipalObjectId
    sqlAdminDisplayName: '${prefix}-sql-admin'
    tags: rg.tags
  }
}

// ─── App Configuration ────────────────────────────────────────────────────────
module appConfig '../modules/appConfiguration.bicep' = {
  name: 'appConfiguration'
  scope: rg
  params: {
    location: location
    appConfigName: appConfigName
    sku: 'standard'
    tags: rg.tags
    initialKeyValues: [
      { key: 'CRM:Environment', value: environment }
      { key: 'CRM:ClientId',    value: clientId    }
      { key: 'CRM:ImageTag',    value: imageTag    }
    ]
  }
}

// ─── Container Apps Environment ───────────────────────────────────────────────
module containerEnv '../modules/containerAppsEnvironment.bicep' = {
  name: 'containerAppsEnvironment'
  scope: rg
  params: {
    location: location
    environmentName: containerEnvName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    logAnalyticsWorkspaceKey: logAnalytics.outputs.sharedKey
    useWorkloadProfile: true
    tags: rg.tags
  }
}

// ─── Container Apps ───────────────────────────────────────────────────────────
var commonEnvVars = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'ConnectionStrings__Default', secretRef: 'sql-connection-string' }
  { name: 'ServiceBus__ConnectionString', secretRef: 'sb-connection-string' }
  { name: 'Entra__TenantId', value: entraTenantId }
  { name: 'Entra__Audience', value: entraAudience }
]

module sfaService '../modules/containerApp.bicep' = {
  name: 'sfaService'
  scope: rg
  params: {
    appName: '${prefix}-sfa'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/sfa-service:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: commonEnvVars
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

module cssService '../modules/containerApp.bicep' = {
  name: 'cssService'
  scope: rg
  params: {
    appName: '${prefix}-css'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/css-service:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: commonEnvVars
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

module marketingService '../modules/containerApp.bicep' = {
  name: 'marketingService'
  scope: rg
  params: {
    appName: '${prefix}-marketing'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/marketing-service:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: commonEnvVars
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

module analyticsService '../modules/containerApp.bicep' = {
  name: 'analyticsService'
  scope: rg
  params: {
    appName: '${prefix}-analytics'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/analytics-service:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: commonEnvVars
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

module staffBff '../modules/containerApp.bicep' = {
  name: 'staffBff'
  scope: rg
  params: {
    appName: '${prefix}-staff-bff'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/staff-bff:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: concat(commonEnvVars, [
      { name: 'ServiceClients__SfaService__BaseAddress',       value: 'https://${sfaService.outputs.fqdn}' }
      { name: 'ServiceClients__CssService__BaseAddress',       value: 'https://${cssService.outputs.fqdn}' }
      { name: 'ServiceClients__MarketingService__BaseAddress', value: 'https://${marketingService.outputs.fqdn}' }
      { name: 'ServiceClients__AnalyticsService__BaseAddress', value: 'https://${analyticsService.outputs.fqdn}' }
    ])
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

module identityService '../modules/containerApp.bicep' = {
  name: 'identityService'
  scope: rg
  params: {
    appName: '${prefix}-identity'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/identity-service:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: commonEnvVars
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

module platformAdminService '../modules/containerApp.bicep' = {
  name: 'platformAdminService'
  scope: rg
  params: {
    appName: '${prefix}-platform-admin'
    location: location
    containerAppsEnvironmentId: containerEnv.outputs.environmentId
    containerImage: '${acr.outputs.loginServer}/platform-admin-service:${imageTag}'
    acrServer: acr.outputs.loginServer
    envVars: commonEnvVars
    minReplicas: 2
    maxReplicas: 20
    cpu: '0.5'
    memory: '1.0Gi'
    externalIngress: false
  }
}

// ─── API Management ───────────────────────────────────────────────────────────
module apim '../modules/apiManagement.bicep' = {
  name: 'apiManagement'
  scope: rg
  params: {
    location: location
    apimName: apimName
    publisherEmail: publisherEmail
    publisherName: publisherName
    sku: 'Premium'
    skuCapacity: 1
    entraTenantId: entraTenantId
    entraAudience: entraAudience
    serviceBackends: {
      sfaService:       sfaService.outputs.fqdn
      cssService:       cssService.outputs.fqdn
      marketingService: marketingService.outputs.fqdn
      analyticsService: analyticsService.outputs.fqdn
      staffBff:         staffBff.outputs.fqdn
      identityService:  identityService.outputs.fqdn
    }
    tags: rg.tags
  }
}

// ─── Static Web Apps ──────────────────────────────────────────────────────────
module staffPortal '../modules/staticWebApp.bicep' = {
  name: 'staffPortal'
  scope: rg
  params: {
    location: location
    staticWebAppName: staffPortalName
    sku: 'Standard'
    tags: rg.tags
  }
}

module customerPortal '../modules/staticWebApp.bicep' = {
  name: 'customerPortal'
  scope: rg
  params: {
    location: location
    staticWebAppName: customerPortalName
    sku: 'Standard'
    tags: rg.tags
  }
}

// ─── Outputs ─────────────────────────────────────────────────────────────────
output resourceGroupName string      = rg.name
output apimGatewayUrl string         = apim.outputs.apimGatewayUrl
output acrLoginServer string         = acr.outputs.loginServer
output containerEnvDomain string     = containerEnv.outputs.defaultDomain
output staffPortalHostname string    = staffPortal.outputs.defaultHostname
output customerPortalHostname string = customerPortal.outputs.defaultHostname
