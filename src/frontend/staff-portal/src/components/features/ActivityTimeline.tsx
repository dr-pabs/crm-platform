import { useTranslation } from 'react-i18next';
import { useActivities } from '../../hooks/useActivities';
import { Spinner, Badge } from '../ui';
import type { Activity, ActivityType } from '../../types';

interface ActivityTimelineProps {
  entityId: string;
  entityType: string;
}

function formatDate(iso: string) {
  return new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(iso));
}

function getTypeVariant(type: ActivityType) {
  switch (type) {
    case 'Call':    return 'info' as const;
    case 'Email':   return 'success' as const;
    case 'Meeting': return 'warning' as const;
    case 'Note':    return 'default' as const;
  }
}

export function ActivityTimeline({ entityId, entityType }: ActivityTimelineProps) {
  const { t } = useTranslation();
  const { data: activities, isLoading } = useActivities(entityId, entityType);

  if (isLoading) return <Spinner size="sm" />;
  if (!activities?.length) return <p className="text-sm text-gray-400">{t('common.noResults')}</p>;

  return (
    <div className="space-y-2">
      {activities.map((a: Activity) => (
        <div key={a.id} className="flex items-start gap-3 rounded-lg border border-gray-200 bg-white p-3">
          <Badge label={a.activityType} variant={getTypeVariant(a.activityType)} />
          <div className="flex-1 min-w-0">
            <p className="text-sm text-gray-700 whitespace-pre-line">{a.notes ?? '—'}</p>
            <div className="mt-1 flex items-center gap-2 text-xs text-gray-400">
              <span>{a.authorName ?? t('common.unknown')}</span>
              <span>·</span>
              <span>{formatDate(a.occurredAt)}</span>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}
