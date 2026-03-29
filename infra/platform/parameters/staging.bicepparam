using '../main.bicep'

param environment = 'staging'
param location = 'uksouth'
param resourceGroupName = 'crm-staging-rg'
param imageTag = 'latest'

// TODO: Add full parameter values per environment.
// See architecture design for SKU specifications per environment tier.
