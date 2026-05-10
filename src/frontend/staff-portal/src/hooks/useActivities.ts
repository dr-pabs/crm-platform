import { useQuery } from '@tanstack/react-query';
import apiClient from '../lib/apiClient';
import type { Activity } from '../types';

export function useActivities(relatedEntityId: string, relatedEntityType: string) {
  return useQuery<Activity[]>({
    queryKey: ['activities', relatedEntityType, relatedEntityId],
    queryFn: async () => {
      const { data } = await apiClient.get<Activity[]>(
        `/activities?relatedEntityId=${relatedEntityId}`,
      );
      return data;
    },
    enabled: !!relatedEntityId,
  });
}
