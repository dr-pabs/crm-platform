import { useParams, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useQuote, useSendQuote, useAcceptQuote, useRejectQuote } from '../hooks/useQuotes';
import { Button, Badge, Spinner } from '../components/ui';
import type { QuoteStatus } from '../types';

const statusVariant: Record<QuoteStatus, 'default' | 'info' | 'success' | 'danger'> = {
  Draft: 'default',
  Sent: 'info',
  Accepted: 'success',
  Rejected: 'danger',
};

export function QuoteDetail() {
  const { id } = useParams<{ id: string }>();
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { data: quote, isLoading } = useQuote(id ?? '');
  const sendQuote = useSendQuote();
  const acceptQuote = useAcceptQuote();
  const rejectQuote = useRejectQuote();

  if (isLoading) return <div className="flex h-64 items-center justify-center"><Spinner size="lg" /></div>;
  if (!quote) return <div className="p-6"><p className="text-gray-500">{t('errors.notFound')}</p></div>;

  const canSend = quote.status === 'Draft';
  const canAccept = quote.status === 'Sent';
  const canReject = quote.status === 'Sent';

  return (
    <div className="p-6 max-w-2xl mx-auto">
      <div className="mb-6 flex items-center gap-4">
        <button onClick={() => { void navigate('/quotes'); }} className="text-sm text-gray-500 hover:text-gray-700">
          {'\u2190'} {t('quotes.title')}
        </button>
        <h1 className="text-2xl font-bold text-gray-900">
          {t('quotes.detailTitle', { id: quote.id.substring(0, 8) })}
        </h1>
        <Badge label={quote.status} variant={statusVariant[quote.status]} />
      </div>

      <div className="rounded-lg border border-gray-200 bg-white p-6 space-y-4">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <span className="text-gray-500">{t('quotes.totalValue')}</span>
            <p className="text-lg font-semibold">
              {new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(quote.totalValue)}
            </p>
          </div>
          <div>
            <span className="text-gray-500">{t('quotes.validUntil')}</span>
            <p>{quote.validUntil ? new Date(quote.validUntil).toLocaleDateString() : '\u2014'}</p>
          </div>
        </div>

        <div>
          <span className="text-sm text-gray-500">{t('quotes.opportunityId')}</span>
          <p className="text-sm font-mono">{quote.opportunityId}</p>
        </div>

        <div>
          <span className="text-sm text-gray-500">{t('common.created')}</span>
          <p className="text-sm">{new Date(quote.createdAt).toLocaleString()}</p>
        </div>

        <div className="flex gap-3 pt-4 border-t">
          {canSend && (
            <Button onClick={() => { sendQuote.mutate(quote.id); }} loading={sendQuote.isPending}>
              {t('quotes.send')}
            </Button>
          )}
          {canAccept && (
            <Button onClick={() => { acceptQuote.mutate(quote.id); }} loading={acceptQuote.isPending}>
              {t('quotes.accept')}
            </Button>
          )}
          {canReject && (
            <Button variant="secondary" onClick={() => { rejectQuote.mutate(quote.id); }} loading={rejectQuote.isPending}>
              {t('quotes.reject')}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
