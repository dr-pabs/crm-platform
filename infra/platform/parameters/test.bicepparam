using '../main.bicep'

param environment = 'test'
param location = 'uksouth'
param resourceGroupName = 'crm-test-rg'
param imageTag = 'latest'

// TODO: Add full parameter values per environment.
// See architecture design for SKU specifications per environment tier.
