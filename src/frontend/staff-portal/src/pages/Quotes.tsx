import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useQuotes, useCreateQuote } from '../hooks/useQuotes';
import { Table, Button, Input, Modal, Badge } from '../components/ui';
import type { Column } from '../components/ui';
import type { Quote, QuoteStatus, CreateQuoteRequest } from '../types';

const statusVariant: Record<QuoteStatus, 'default' | 'info' | 'success' | 'warning' | 'danger'> = {
  Draft: 'default',
  Sent: 'info',
  Accepted: 'success',
  Rejected: 'danger',
};

const createSchema = z.object({
  opportunityId: z.string().uuid(),
  totalValue: z.number().min(0),
  validUntil: z.string().optional(),
});

type CreateFormValues = z.infer<typeof createSchema>;

export function Quotes() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [createOpen, setCreateOpen] = useState(false);

  const { data, isLoading } = useQuotes();
  const createQuote = useCreateQuote();

  const { register, handleSubmit, reset, formState: { errors } } = useForm<CreateFormValues>({
    resolver: zodResolver(createSchema),
  });

  const onCreateSubmit = (values: CreateFormValues) => {
    const req: CreateQuoteRequest = values;
    createQuote.mutate(req, {
      onSuccess: () => { setCreateOpen(false); reset(); },
    });
  };

  const columns: Column<Quote>[] = [
    {
      key: 'opportunityId',
      header: t('quotes.opportunity'),
      render: (row) => row.opportunityId.substring(0, 8) + '...',
    },
    {
      key: 'totalValue',
      header: t('quotes.totalValue'),
      render: (row) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(row.totalValue),
    },
    {
      key: 'status',
      header: t('quotes.status'),
      render: (row) => <Badge label={row.status} variant={statusVariant[row.status]} />,
    },
    {
      key: 'validUntil',
      header: t('quotes.validUntil'),
      render: (row) => (row.validUntil ? new Date(row.validUntil).toLocaleDateString() : '\u2014'),
    },
    {
      key: 'createdAt',
      header: t('common.created'),
      render: (row) => new Date(row.createdAt).toLocaleDateString(),
    },
  ];

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">{t('quotes.title')}</h1>
        <Button onClick={() => { setCreateOpen(true); }}>{t('quotes.newQuote')}</Button>
      </div>

      <Table
        columns={columns}
        data={data?.items ?? []}
        loading={isLoading}
        emptyMessage={t('common.noResults')}
        onRowClick={(row) => { void navigate(`/quotes/${row.id}`); }}
      />

      <Modal
        open={createOpen}
        onClose={() => { setCreateOpen(false); reset(); }}
        title={t('quotes.newQuote')}
        footer={
          <>
            <Button variant="secondary" onClick={() => { setCreateOpen(false); reset(); }}>
              {t('common.cancel')}
            </Button>
            <Button form="create-quote-form" type="submit" loading={createQuote.isPending}>
              {t('common.create')}
            </Button>
          </>
        }
      >
        <form
          id="create-quote-form"
          onSubmit={(e) => { void handleSubmit(onCreateSubmit)(e); }}
          className="space-y-4"
        >
          <Input label={t('quotes.opportunityId')} required {...register('opportunityId')} error={errors.opportunityId?.message} />
          <Input label={t('quotes.totalValue')} type="number" required {...register('totalValue', { valueAsNumber: true })} error={errors.totalValue?.message} />
          <Input label={t('quotes.validUntil')} type="date" {...register('validUntil')} />
        </form>
      </Modal>
    </div>
  );
}
