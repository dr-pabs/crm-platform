import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { useTranslation } from 'react-i18next';
import {
  usePromptTemplates,
  useCreatePromptTemplate,
  useUpdatePromptTemplate,
  useDeletePromptTemplate,
} from '../../hooks/useAiJobs';
import { Table, Button, Modal, Input, ConfirmModal, Badge } from '../../components/ui';
import type { Column } from '../../components/ui';
import type { PromptTemplate, CapabilityType, PaginationParams } from '../../types';

const CAPABILITY_OPTIONS: CapabilityType[] = [
  'Summarisation',
  'SentimentAnalysis',
  'LeadScoring',
  'NextBestAction',
  'DraftGeneration',
  'JourneyPersonalisation',
  'Forecasting',
  'ChurnPrediction',
  'ContentGeneration',
];

const templateSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  capability: z.enum([
    'Summarisation',
    'SentimentAnalysis',
    'LeadScoring',
    'NextBestAction',
    'DraftGeneration',
    'JourneyPersonalisation',
    'Forecasting',
    'ChurnPrediction',
    'ContentGeneration',
  ] as const),
  useCase: z.string().min(1, 'Use case is required'),
  templateBody: z.string().min(1, 'Template body is required'),
});

type TemplateFormValues = z.infer<typeof templateSchema>;

export function PromptTemplates() {
  const { t } = useTranslation();
  const [params] = useState<PaginationParams>({ page: 1, pageSize: 50 });
  const [createOpen, setCreateOpen] = useState(false);
  const [editTemplate, setEditTemplate] = useState<PromptTemplate | null>(null);
  const [deleteId, setDeleteId] = useState<string | null>(null);

  const { data, isLoading } = usePromptTemplates(params);
  const createTemplate = useCreatePromptTemplate();
  const updateTemplate = useUpdatePromptTemplate(editTemplate?.id ?? '');
  const deleteTemplate = useDeletePromptTemplate();

  const {
    register: registerCreate,
    handleSubmit: handleCreate,
    reset: resetCreate,
    formState: { errors: createErrors },
  } = useForm<TemplateFormValues>({ resolver: zodResolver(templateSchema) });

  const {
    register: registerEdit,
    handleSubmit: handleEdit,
    reset: resetEdit,
    formState: { errors: editErrors },
  } = useForm<Pick<TemplateFormValues, 'name' | 'templateBody'>>({
    resolver: zodResolver(templateSchema.pick({ name: true, templateBody: true })),
  });

  const onCreateSubmit = (values: TemplateFormValues) => {
    createTemplate.mutate(values, {
      onSuccess: () => {
        setCreateOpen(false);
        resetCreate();
      },
    });
  };

  const onEditSubmit = (values: Pick<TemplateFormValues, 'name' | 'templateBody'>) => {
    updateTemplate.mutate(values, {
      onSuccess: () => {
        setEditTemplate(null);
        resetEdit();
      },
    });
  };

  const openEdit = (template: PromptTemplate) => {
    setEditTemplate(template);
    resetEdit({ name: template.name, templateBody: template.templateBody });
  };

  const columns: Column<PromptTemplate>[] = [
    { key: 'name', header: t('settings.promptTemplates.name') },
    {
      key: 'capability',
      header: t('settings.promptTemplates.capability'),
      render: (row) => <Badge label={row.capability} variant="info" />,
    },
    { key: 'useCase', header: t('settings.promptTemplates.useCase') },
    {
      key: 'isSystemDefault',
      header: t('settings.promptTemplates.systemDefault'),
      render: (row) => (
        <Badge
          label={row.isSystemDefault ? t('common.yes') : t('common.no')}
          variant={row.isSystemDefault ? 'success' : 'default'}
        />
      ),
    },
    {
      key: 'updatedAt',
      header: t('common.updated'),
      render: (row) => new Date(row.updatedAt).toLocaleDateString(),
    },
    {
      key: 'actions',
      header: t('common.actions'),
      render: (row) => (
        <div className="flex gap-2">
          {!row.isSystemDefault && (
            <>
              <button
                onClick={(e: { stopPropagation: () => void }) => { e.stopPropagation(); openEdit(row); }}
                className="text-xs text-blue-600 hover:text-blue-800"
              >
                {t('common.edit')}
              </button>
              <button
                onClick={(e: { stopPropagation: () => void }) => { e.stopPropagation(); setDeleteId(row.id); }}
                className="text-xs text-red-500 hover:text-red-700"
              >
                {t('common.delete')}
              </button>
            </>
          )}
        </div>
      ),
    },
  ];

  return (
    <div className="p-6">
      <div className="mb-6 flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{t('settings.promptTemplates.title')}</h1>
          <p className="mt-1 text-sm text-gray-500">{t('settings.promptTemplates.subtitle')}</p>
        </div>
        <Button onClick={() => { setCreateOpen(true); }}>{t('settings.promptTemplates.create')}</Button>
      </div>

      <Table
        columns={columns}
        data={data?.items ?? []}
        loading={isLoading}
        emptyMessage={t('common.noResults')}
      />

      {/* Create Modal */}
      <Modal
        open={createOpen}
        onClose={() => { setCreateOpen(false); resetCreate(); }}
        title={t('settings.promptTemplates.create')}
      >
        <form onSubmit={handleCreate(onCreateSubmit)} className="space-y-4">
          <Input
            label={t('settings.promptTemplates.name')}
            {...registerCreate('name')}
            error={createErrors.name?.message}
          />
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('settings.promptTemplates.capability')}
            </label>
            <select
              {...registerCreate('capability')}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary-500"
            >
              {CAPABILITY_OPTIONS.map((c) => (
                <option key={c} value={c}>{c}</option>
              ))}
            </select>
            {createErrors.capability && (
              <p className="mt-1 text-xs text-red-600">{createErrors.capability.message}</p>
            )}
          </div>
          <Input
            label={t('settings.promptTemplates.useCase')}
            {...registerCreate('useCase')}
            error={createErrors.useCase?.message}
          />
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('settings.promptTemplates.templateBody')}
            </label>
            <textarea
              {...registerCreate('templateBody')}
              rows={6}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
            {createErrors.templateBody && (
              <p className="mt-1 text-xs text-red-600">{createErrors.templateBody.message}</p>
            )}
          </div>
          <div className="flex justify-end gap-3">
            <Button variant="secondary" type="button" onClick={() => { setCreateOpen(false); resetCreate(); }}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={createTemplate.isPending}>{t('common.save')}</Button>
          </div>
        </form>
      </Modal>

      {/* Edit Modal */}
      <Modal
        open={editTemplate !== null}
        onClose={() => { setEditTemplate(null); }}
        title={t('settings.promptTemplates.edit')}
      >
        <form onSubmit={handleEdit(onEditSubmit)} className="space-y-4">
          <Input
            label={t('settings.promptTemplates.name')}
            {...registerEdit('name')}
            error={editErrors.name?.message}
          />
          <div>
            <label className="mb-1 block text-sm font-medium text-gray-700">
              {t('settings.promptTemplates.templateBody')}
            </label>
            <textarea
              {...registerEdit('templateBody')}
              rows={6}
              className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm font-mono focus:outline-none focus:ring-2 focus:ring-primary-500"
            />
            {editErrors.templateBody && (
              <p className="mt-1 text-xs text-red-600">{editErrors.templateBody.message}</p>
            )}
          </div>
          <div className="flex justify-end gap-3">
            <Button variant="secondary" type="button" onClick={() => { setEditTemplate(null); }}>
              {t('common.cancel')}
            </Button>
            <Button type="submit" loading={updateTemplate.isPending}>{t('common.save')}</Button>
          </div>
        </form>
      </Modal>

      <ConfirmModal
        open={deleteId !== null}
        onClose={() => { setDeleteId(null); }}
        onConfirm={() => {
          if (deleteId) deleteTemplate.mutate(deleteId, { onSuccess: () => { setDeleteId(null); } });
        }}
        title={t('settings.promptTemplates.delete')}
        message={t('settings.promptTemplates.deleteConfirm')}
        confirmLabel={t('common.delete')}
        loading={deleteTemplate.isPending}
      />
    </div>
  );
}
