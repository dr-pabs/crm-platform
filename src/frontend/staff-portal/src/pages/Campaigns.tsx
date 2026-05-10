import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { zodResolver } from '@hookform/resolvers/zod';
import { useCampaigns, useCreateCampaign, useDeleteCampaign } from '../hooks/useCampaigns';
import { Table, Button, Input, Modal, Badge, ConfirmModal } from '../components/ui';
import type { Column } from '../components/ui';
import type { Campaign, CampaignStatus, CreateCampaignRequest, PaginationParams } from '../types';

const statusVariant: Record<CampaignStatus, 'default' | 'info' | 'success' | 'warning' | 'danger'> = {
  Draft: 'default',
  Scheduled: 'info',
  Active: 'success',
  Paused: 'warning',
  Completed: 'info',
  Cancelled: 'danger',
};

const createSchema = z.object({
  name: z.string().min(1),
  description: z.string().optional(),
  channel: z.string().optional(),
});

type CreateFormValues = z.infer<typeof createSchema>;

export function Campaigns() {
  const { t } = useTranslation();
  const [params, setParams] = useState<PaginationParams>({ page: 1, pageSize: 20 });
  const [createOpen, setCreateOpen] = useState(false);
  const [deleteId, setDeleteId] = useState<string | null>(null);

  const { data, isLoading } = useCampaigns(params);
  const createCampaign = useCreateCampaign();
  const deleteCampaign = useDeleteCampaign();

  const { register, handleSubmit, reset, formState: { errors } } = useForm<CreateFormValues>({
    resolver: zodResolver(createSchema),
  });

  const onCreateSubmit = (values: CreateFormValues) => {
    const req: CreateCampaignRequest = values;
    createCampaign.mutate(req, {
      onSuccess: () => { setCreateOpen(false); reset(); },
    });
  };

  const columns: Column<Campaign>[] = [
    { key: 'name', header: t('campaigns.name') },
    {
      key: 'status',
      header: t('campaigns.status'),
      render: (row) => <Badge label={row.status} variant={statusVariant[row.status]} />,
    },
    {
      key: 'impressions',
      header: t('campaigns.impressions'),
      render: (row) => row.impressions?.toLocaleString() ?? '\u2014',
    },
    {
      key: 'clicks',
      header: t('campaigns.clicks'),
      render: (row) => row.clicks?.toLocaleString() ?? '\u2014',
    },
    {
      key: 'conversions',
      header: t('campaigns.conversions'),
      render: (row) => row.conversions?.toLocaleString() ?? '\u2014',
    },
    {
      key: 'startDate',
      header: t('campaigns.startDate'),
      render: (row) => (row.startDate ? new Date(row.startDate).toLocaleDateString() : '\u2014'),
    },
    {
      key: 'actions',
      header: t('common.actions'),
      render: (row) => (
        <button
          onClick={(e: { stopPropagation: () => void }) => { e.stopPropagation(); setDeleteId(row.id); }}
          className="text-xs text-red-500 hover:text-red-700"
        >
          {t('common.delete')}
        </button>
      ),
    },
  ];

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">{t('campaigns.title')}</h1>
        <Button onClick={() => { setCreateOpen(true); }}>{t('campaigns.newCampaign')}</Button>
      </div>

      <Table
        columns={columns}
        data={data?.items ?? []}
        loading={isLoading}
        emptyMessage={t('common.noResults')}
      />

      {data && data.totalPages > 1 && (
        <div className="mt-4 flex justify-end gap-2">
          <Button size="sm" variant="secondary" disabled={params.page <= 1}
            onClick={() => { setParams((p) => ({ ...p, page: p.page - 1 })); }}>
            {t('common.previous')}
          </Button>
          <Button size="sm" variant="secondary" disabled={params.page >= data.totalPages}
            onClick={() => { setParams((p) => ({ ...p, page: p.page + 1 })); }}>
            {t('common.next')}
          </Button>
        </div>
      )}

      <Modal
        open={createOpen}
        onClose={() => { setCreateOpen(false); reset(); }}
        title={t('campaigns.newCampaign')}
        footer={
          <>
            <Button variant="secondary" onClick={() => { setCreateOpen(false); reset(); }}>
              {t('common.cancel')}
            </Button>
            <Button form="create-campaign-form" type="submit" loading={createCampaign.isPending}>
              {t('common.create')}
            </Button>
          </>
        }
      >
        <form
          id="create-campaign-form"
          onSubmit={(e) => { void handleSubmit(onCreateSubmit)(e); }}
          className="space-y-4"
        >
          <Input label={t('campaigns.name')} required {...register('name')} error={errors.name?.message} />
          <Input label={t('campaigns.description')} {...register('description')} />
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">{t('campaigns.channel')}</label>
            <select {...register('channel')} className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500">
              <option value="">{t('common.select')}</option>
              <option value="Email">Email</option>
              <option value="Sms">SMS</option>
              <option value="InApp">In-App</option>
              <option value="Push">Push</option>
            </select>
          </div>
        </form>
      </Modal>

      <ConfirmModal
        open={deleteId !== null}
        onClose={() => { setDeleteId(null); }}
        onConfirm={() => {
          if (deleteId) deleteCampaign.mutate(deleteId, { onSuccess: () => { setDeleteId(null); } });
        }}
        title={t('campaigns.deleteCampaign')}
        message={t('campaigns.deleteConfirm')}
        confirmLabel={t('common.delete')}
        loading={deleteCampaign.isPending}
      />
    </div>
  );
}
