import { useTranslation } from 'react-i18next';
import { Badge, Spinner } from '../../components/ui';
import type { BadgeVariant } from '../../components/ui';

// Static connector definitions — status is read from integration-service health
// in a real implementation; shown as a configurable placeholder here.
interface ConnectorInfo {
  id: string;
  name: string;
  description: string;
  status: 'Connected' | 'Disconnected' | 'Error' | 'Pending';
  lastSyncAt?: string;
}

const statusVariant: Record<ConnectorInfo['status'], BadgeVariant> = {
  Connected: 'success',
  Disconnected: 'default',
  Error: 'danger',
  Pending: 'warning',
};

const CONNECTORS: ConnectorInfo[] = [
  {
    id: 'sendgrid',
    name: 'SendGrid',
    description: 'Email delivery for campaigns and transactional notifications.',
    status: 'Connected',
    lastSyncAt: new Date().toISOString(),
  },
  {
    id: 'twilio',
    name: 'Twilio',
    description: 'SMS delivery for cases and notification alerts.',
    status: 'Connected',
    lastSyncAt: new Date().toISOString(),
  },
  {
    id: 'teams',
    name: 'Microsoft Teams',
    description: 'Collaboration notifications via Teams webhooks.',
    status: 'Pending',
  },
  {
    id: 'azure-openai',
    name: 'Azure OpenAI',
    description: 'AI capabilities — summarisation, scoring, draft generation.',
    status: 'Connected',
    lastSyncAt: new Date().toISOString(),
  },
  {
    id: 'service-bus',
    name: 'Azure Service Bus',
    description: 'Async message bus for AI job queuing and integration events.',
    status: 'Connected',
    lastSyncAt: new Date().toISOString(),
  },
];

export function Connectors() {
  const { t } = useTranslation();
  const isLoading = false;

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-900">{t('settings.connectors.title')}</h1>
        <p className="mt-1 text-sm text-gray-500">{t('settings.connectors.subtitle')}</p>
      </div>

      {isLoading ? (
        <div className="flex h-32 items-center justify-center">
          <Spinner size="lg" />
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {CONNECTORS.map((connector) => (
            <div
              key={connector.id}
              className="flex items-start gap-4 rounded-lg border border-gray-200 bg-white p-5 shadow-sm"
            >
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-gray-900">{connector.name}</h3>
                  <Badge label={connector.status} variant={statusVariant[connector.status]} />
                </div>
                <p className="mt-1 text-sm text-gray-500">{connector.description}</p>
                {connector.lastSyncAt && (
                  <p className="mt-2 text-xs text-gray-400">
                    {t('settings.connectors.lastSync')}: {new Date(connector.lastSyncAt).toLocaleString()}
                  </p>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
