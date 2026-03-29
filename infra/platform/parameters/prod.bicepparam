using '../main.bicep'

param environment = 'prod'
param location = 'uksouth'
param resourceGroupName = 'crm-prod-rg'
param imageTag = 'latest'

// TODO: Add full parameter values per environment.
// See architecture design for SKU specifications per environment tier.
