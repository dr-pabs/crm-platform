import { Configuration, LogLevel } from '@azure/msal-browser';

const configuredClientId = import.meta.env.VITE_AZURE_CLIENT_ID;
const configuredTenantId = import.meta.env.VITE_AZURE_TENANT_ID;
const authorityBase = import.meta.env.VITE_AZURE_AUTHORITY || 'https://login.microsoftonline.com/';

export const isAuthConfigured = Boolean(configuredClientId && configuredTenantId);

if (!isAuthConfigured && !import.meta.env.DEV) {
  throw new Error('VITE_AZURE_CLIENT_ID and VITE_AZURE_TENANT_ID must be set in environment');
}

const clientId = configuredClientId || '00000000-0000-0000-0000-000000000000';
const tenantId = configuredTenantId || 'common';
const authority = `${authorityBase}${tenantId}`;

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority,
    redirectUri: window.location.origin,
    postLogoutRedirectUri: window.location.origin,
  },
  cache: {
    cacheLocation: 'sessionStorage', // sessionStorage — never localStorage
    storeAuthStateInCookie: false,
  },
  system: {
    loggerOptions: {
      loggerCallback: (level: LogLevel, message: string, containsPii: boolean) => {
        if (containsPii) return; // never log PII
        if (import.meta.env.DEV) {
          switch (level) {
            case LogLevel.Error:
              console.error(message);
              break;
            case LogLevel.Warning:
              console.warn(message);
              break;
            case LogLevel.Info:
              console.info(message);
              break;
            default:
              break;
          }
        }
      },
      piiLoggingEnabled: false,
    },
  },
};

/** Scopes required to call the CRM API. */
export const apiScopes: string[] = isAuthConfigured ? [`api://${clientId}/crm.access`] : [];

/** Login request — used for interactive login. */
export const loginRequest = {
  scopes: apiScopes,
};
