import { useTranslation } from 'react-i18next';
import { Spinner } from '../components/ui';
import { useAnalyticsDashboard } from '../hooks/useAnalyticsMetrics';
import type { AnalyticsMetric } from '../types';

function MetricCard({ metric }: { metric: AnalyticsMetric }) {
  const isPositive = metric.delta !== undefined && metric.delta >= 0;
  const deltaColor = isPositive ? 'text-green-600' : 'text-red-600';
  const deltaPrefix = isPositive ? '+' : '';

  return (
    <div className="rounded-lg border border-border bg-white p-5 shadow-sm">
      <p className="text-xs font-medium uppercase tracking-wide text-foreground-muted">{metric.metricName}</p>
      <p className="mt-2 text-3xl font-bold text-foreground">
        {typeof metric.value === 'number' ? metric.value.toLocaleString() : metric.value}
      </p>
      {metric.delta !== undefined && (
        <p className={`mt-1 text-sm ${deltaColor}`}>
          {deltaPrefix}{metric.delta} vs previous period
        </p>
      )}
      <p className="mt-1 text-xs text-foreground-muted">{metric.period}</p>
    </div>
  );
}

export function Analytics() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useAnalyticsDashboard();

  return (
    <div className="p-6">
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-foreground">{t('analytics.title')}</h1>
        <p className="mt-1 text-sm text-foreground-muted">{t('analytics.subtitle')}</p>
      </div>

      {isLoading && (
        <div className="flex h-32 items-center justify-center">
          <Spinner size="lg" />
        </div>
      )}

      {isError && (
        <div className="rounded-md bg-red-50 p-4 text-sm text-red-700">
          {t('analytics.loadError')}
        </div>
      )}

      {data && (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data.metrics.map((metric) => (
            <MetricCard key={metric.metricName} metric={metric} />
          ))}
        </div>
      )}
    </div>
  );
}
