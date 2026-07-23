import { useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { invoiceDetailClient } from '@/api/invoiceDetailClient';
import type { InvoiceDetail } from '@/types/invoiceDetail';

interface InvoiceDetailState {
  invoice: InvoiceDetail | null;
  isLoading: boolean;
  error: string | null;
  notFound: boolean;
  retry: () => void;
}

/** Loads a single invoice's full detail for the Review Screen (WP-016). */
export function useInvoiceDetail(invoiceId: string | undefined): InvoiceDetailState {
  const { user } = useAuth();
  const [invoice, setInvoice] = useState<InvoiceDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    if (!user || !invoiceId) {
      // No user/invoiceId yet - the early return below overrides isLoading
      // directly rather than this effect resetting it, so there is nothing
      // to synchronize here.
      return;
    }

    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);
    setNotFound(false);

    invoiceDetailClient
      .getInvoiceDetail(user.tenantId, invoiceId)
      .then((result) => {
        if (cancelled) return;
        if (result) {
          setInvoice(result);
        } else {
          setInvoice(null);
          setNotFound(true);
        }
        setIsLoading(false);
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load this invoice. Please try again.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [user, invoiceId, reloadToken]);

  const noSubject = !user || !invoiceId;

  return {
    invoice: noSubject ? null : invoice,
    isLoading: noSubject ? false : isLoading,
    error: noSubject ? null : error,
    notFound: noSubject ? false : notFound,
    retry: () => setReloadToken((t) => t + 1),
  };
}
