// CRM Platform — Main Bicep entry point
// Orchestrates all platform modules for a given environment.
// Deploy via: az deployment sub create --location uksouth --template-file main.bicep --parameters @parameters/prod.bicepparam

targetScope = 'subscription'

@description('Environment name (dev | test | staging | prod)')
@allowed(['dev', 'test', 'staging', 'prod'])
param environment string

@description('Azure region for all resources')
param location string = 'uksouth'

@description('Resource group name')
param resourceGroupName string = 'crm-${environment}-rg'

@description('Container image tag to deploy')
param imageTag string

// TODO: Full module orchestration to be implemented by DevOps engineer.
// See architecture design document for full resource list and SKU configuration per environment.

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: resourceGroupName
  location: location
  tags: {
    environment: environment
    project: 'crm-platform'
    managedBy: 'bicep'
  }
}
