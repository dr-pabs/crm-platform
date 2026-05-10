import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import apiClient from '../lib/apiClient';
import type { Quote, CreateQuoteRequest, PagedResult } from '../types';

const QUOTES_KEY = 'quotes' as const;

export function useQuotes(opportunityId?: string) {
  return useQuery({
    queryKey: [QUOTES_KEY, { opportunityId }],
    queryFn: async () => {
      const params: Record<string, string> = {};
      if (opportunityId) params.opportunityId = opportunityId;
      const { data } = await apiClient.get<PagedResult<Quote>>('/quotes', { params });
      return data;
    },
  });
}

export function useQuote(id: string) {
  return useQuery({
    queryKey: [QUOTES_KEY, id],
    queryFn: async () => {
      const { data } = await apiClient.get<Quote>(`/quotes/${id}`);
      return data;
    },
    enabled: !!id,
  });
}

export function useCreateQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (req: CreateQuoteRequest) => {
      const { data } = await apiClient.post<Quote>('/quotes', req);
      return data;
    },
    onSuccess: () => { void qc.invalidateQueries({ queryKey: [QUOTES_KEY] }); },
  });
}

export function useSendQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.post(`/quotes/${id}/send`);
    },
    onSuccess: () => { void qc.invalidateQueries({ queryKey: [QUOTES_KEY] }); },
  });
}

export function useAcceptQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.post(`/quotes/${id}/accept`);
    },
    onSuccess: () => { void qc.invalidateQueries({ queryKey: [QUOTES_KEY] }); },
  });
}

export function useRejectQuote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => {
      await apiClient.post(`/quotes/${id}/reject`);
    },
    onSuccess: () => { void qc.invalidateQueries({ queryKey: [QUOTES_KEY] }); },
  });
}
