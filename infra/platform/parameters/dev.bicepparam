using '../main.bicep'

param environment = 'dev'
param location = 'uksouth'
param resourceGroupName = 'crm-dev-rg'
param imageTag = 'latest'

// TODO: Add full parameter values per environment.
// See architecture design for SKU specifications per environment tier.
