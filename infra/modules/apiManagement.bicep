// infra/modules/apiManagement.bicep
// Azure API Management — Layer 1 auth (JWT signature validation) + routing.
// ADR 0006: APIM is the single entry point for all external traffic.

@description('Azure region')
param location string

@description('APIM service name (globally unique)')
param apimName string

@description('Publisher email')
param publisherEmail string

@description('Publisher organisation name')
param publisherName string

@description('SKU — Developer (no SLA) for dev, Premium for prod (multi-region, VNet)')
@allowed(['Developer', 'Basic', 'Standard', 'Premium'])
param sku string = 'Developer'

@description('SKU capacity units')
param skuCapacity int = 1

@description('Entra ID tenant ID for JWT validation policy')
param entraTenantId string

@description('Entra ID audience (App Registration client ID)')
param entraAudience string

@description('''
Map of service name → backend FQDN.
Keys must match the services array used in containerApp modules.
Example: { sfaService: 'sfa-service.internal.example.com', ... }
''')
param serviceBackends object = {}

@description('Tags')
param tags object = {}

// ─── APIM Service ─────────────────────────────────────────────────────────────
resource apim 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: apimName
  location: location
  tags: tags
  sku: {
    name: sku
    capacity: skuCapacity
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
    virtualNetworkType: sku == 'Premium' ? 'Internal' : 'None'
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Ssl30': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls10': 'false'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls11': 'false'
    }
  }
}

// ─── Global inbound policy — JWT validation (Layer 1 auth, ADR 0004) ──────────
resource globalPolicy 'Microsoft.ApiManagement/service/policies@2023-05-01-preview' = {
  parent: apim
  name: 'policy'
  properties: {
    value: loadTextContent('../policies/apim-global-policy.xml')
    format: 'xml'
  }
}

// ─── Named values — used by per-API policies ──────────────────────────────────
resource nvEntraTenantId 'Microsoft.ApiManagement/service/namedValues@2023-05-01-preview' = {
  parent: apim
  name: 'entra-tenant-id'
  properties: {
    displayName: 'EntraTenantId'
    value: entraTenantId
    secret: false
  }
}

resource nvEntraAudience 'Microsoft.ApiManagement/service/namedValues@2023-05-01-preview' = {
  parent: apim
  name: 'entra-audience'
  properties: {
    displayName: 'EntraAudience'
    value: entraAudience
    secret: false
  }
}

// ─── Products ─────────────────────────────────────────────────────────────────

resource staffProduct 'Microsoft.ApiManagement/service/products@2023-05-01-preview' = {
  parent: apim
  name: 'staff'
  properties: {
    displayName: 'Staff Portal'
    description: 'APIs accessible by authenticated CRM staff (Entra ID)'
    subscriptionRequired: false
    state: 'published'
    approvalRequired: false
  }
}

resource customerProduct 'Microsoft.ApiManagement/service/products@2023-05-01-preview' = {
  parent: apim
  name: 'customer'
  properties: {
    displayName: 'Customer Portal'
    description: 'APIs accessible by authenticated customers (Entra External ID)'
    subscriptionRequired: false
    state: 'published'
    approvalRequired: false
  }
}

// ─── Backends — one per service ───────────────────────────────────────────────
// serviceBackends is passed in from main.bicep once Container Apps FQDNs are known.

resource sfaBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = if (contains(serviceBackends, 'sfaService')) {
  parent: apim
  name: 'sfa-service'
  properties: {
    url: 'https://${serviceBackends.sfaService}'
    protocol: 'http'
    tls: { validateCertificateChain: true, validateCertificateName: true }
  }
}

resource cssBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = if (contains(serviceBackends, 'cssService')) {
  parent: apim
  name: 'css-service'
  properties: {
    url: 'https://${serviceBackends.cssService}'
    protocol: 'http'
    tls: { validateCertificateChain: true, validateCertificateName: true }
  }
}

resource marketingBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = if (contains(serviceBackends, 'marketingService')) {
  parent: apim
  name: 'marketing-service'
  properties: {
    url: 'https://${serviceBackends.marketingService}'
    protocol: 'http'
    tls: { validateCertificateChain: true, validateCertificateName: true }
  }
}

resource analyticsBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = if (contains(serviceBackends, 'analyticsService')) {
  parent: apim
  name: 'analytics-service'
  properties: {
    url: 'https://${serviceBackends.analyticsService}'
    protocol: 'http'
    tls: { validateCertificateChain: true, validateCertificateName: true }
  }
}

resource staffBffBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = if (contains(serviceBackends, 'staffBff')) {
  parent: apim
  name: 'staff-bff'
  properties: {
    url: 'https://${serviceBackends.staffBff}'
    protocol: 'http'
    tls: { validateCertificateChain: true, validateCertificateName: true }
  }
}

resource identityBackend 'Microsoft.ApiManagement/service/backends@2023-05-01-preview' = if (contains(serviceBackends, 'identityService')) {
  parent: apim
  name: 'identity-service'
  properties: {
    url: 'https://${serviceBackends.identityService}'
    protocol: 'http'
    tls: { validateCertificateChain: true, validateCertificateName: true }
  }
}

// ─── APIs ─────────────────────────────────────────────────────────────────────

resource sfaApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apim
  name: 'sfa-api'
  properties: {
    displayName: 'Sales Force Automation'
    description: 'Leads, opportunities, contacts, accounts, activities'
    path: 'sfa'
    protocols: ['https']
    serviceUrl: contains(serviceBackends, 'sfaService') ? 'https://${serviceBackends.sfaService}' : null
    subscriptionRequired: false
    isCurrent: true
  }
}

resource sfaApiStaffProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: staffProduct
  name: sfaApi.name
}

resource cssApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apim
  name: 'css-api'
  properties: {
    displayName: 'Customer Support'
    description: 'Cases, SLA policies, comments, escalations'
    path: 'css'
    protocols: ['https']
    serviceUrl: contains(serviceBackends, 'cssService') ? 'https://${serviceBackends.cssService}' : null
    subscriptionRequired: false
    isCurrent: true
  }
}

resource cssApiStaffProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: staffProduct
  name: cssApi.name
}

resource cssApiCustomerProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: customerProduct
  name: cssApi.name
}

resource marketingApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apim
  name: 'marketing-api'
  properties: {
    displayName: 'Marketing Automation'
    description: 'Campaigns, journeys, enrollments, email templates'
    path: 'marketing'
    protocols: ['https']
    serviceUrl: contains(serviceBackends, 'marketingService') ? 'https://${serviceBackends.marketingService}' : null
    subscriptionRequired: false
    isCurrent: true
  }
}

resource marketingApiStaffProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: staffProduct
  name: marketingApi.name
}

resource analyticsApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apim
  name: 'analytics-api'
  properties: {
    displayName: 'Analytics'
    description: 'Dashboard metrics, time series, event log'
    path: 'analytics'
    protocols: ['https']
    serviceUrl: contains(serviceBackends, 'analyticsService') ? 'https://${serviceBackends.analyticsService}' : null
    subscriptionRequired: false
    isCurrent: true
  }
}

resource analyticsApiStaffProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: staffProduct
  name: analyticsApi.name
}

resource staffBffApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apim
  name: 'staff-bff-api'
  properties: {
    displayName: 'Staff BFF'
    description: 'Aggregated dashboard and lead payloads for the staff portal (ADR 0005)'
    path: 'bff'
    protocols: ['https']
    serviceUrl: contains(serviceBackends, 'staffBff') ? 'https://${serviceBackends.staffBff}' : null
    subscriptionRequired: false
    isCurrent: true
  }
}

resource staffBffApiStaffProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: staffProduct
  name: staffBffApi.name
}

resource identityApi 'Microsoft.ApiManagement/service/apis@2023-05-01-preview' = {
  parent: apim
  name: 'identity-api'
  properties: {
    displayName: 'Identity'
    description: 'User provisioning and profile management'
    path: 'identity'
    protocols: ['https']
    serviceUrl: contains(serviceBackends, 'identityService') ? 'https://${serviceBackends.identityService}' : null
    subscriptionRequired: false
    isCurrent: true
  }
}

resource identityApiStaffProduct 'Microsoft.ApiManagement/service/products/apis@2023-05-01-preview' = {
  parent: staffProduct
  name: identityApi.name
}

// ─── Outputs ──────────────────────────────────────────────────────────────────

output apimId string = apim.id
output apimGatewayUrl string = apim.properties.gatewayUrl
output apimPrincipalId string = apim.identity.principalId
