import { useQuery } from '@tanstack/react-query';
import apiClient from '../lib/apiClient';
import type { AnalyticsMetric } from '../types';

const ANALYTICS_KEY = 'analytics' as const;

export interface AnalyticsDashboardResponse {
  metrics: AnalyticsMetric[];
  generatedAt: string;
}

export function useAnalyticsDashboard() {
  return useQuery({
    queryKey: [ANALYTICS_KEY, 'dashboard'],
    queryFn: async () => {
      const response = await apiClient.get<AnalyticsDashboardResponse>('/analytics/dashboard');
      return response.data;
    },
    staleTime: 5 * 60 * 1000, // 5 minutes — analytics data doesn't need real-time freshness
  });
}

export function useAnalyticsMetric(key: string) {
  return useQuery({
    queryKey: [ANALYTICS_KEY, 'metric', key],
    queryFn: async () => {
      const response = await apiClient.get<AnalyticsMetric>(`/analytics/metrics/${key}`);
      return response.data;
    },
    enabled: Boolean(key),
    staleTime: 5 * 60 * 1000,
  });
}
