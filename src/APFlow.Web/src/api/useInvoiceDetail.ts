import { useEffect, useRef, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { invoiceDetailClient } from '@/api/invoiceDetailClient';
import type { InvoiceDetail } from '@/types/invoiceDetail';

interface InvoiceDetailState {
  invoice: InvoiceDetail | null;
  isLoading: boolean;
  error: string | null;
  notFound: boolean;
  retry: () => void;
  /**
   * Applies an already-fetched, fresher InvoiceDetail directly to this
   * hook's state — used by WorkflowActionsPanel after a successful status
   * change, since WP-054's PATCH endpoint already returns the full
   * updated InvoiceDetail (WP-020 task 5 / docs/Backlog.md: "use WP-054's
   * full InvoiceDetail response directly, not a second retry()"). Avoids
   * an otherwise-redundant second network round trip.
   */
  applyUpdatedInvoice: (updated: InvoiceDetail) => void;
}

/**
 * Loads a single invoice's full detail for the Review Screen (WP-016).
 *
 * Revokes the previous invoice's `pdfUrl` if it's a `blob:` object URL
 * (created by `HttpInvoiceDetailClient` as of WP-020, to work around
 * `<object>`/`<a>` not being able to attach an Authorization header —
 * see invoiceDetailClient.ts's own doc comment) whenever a new invoice
 * loads or this hook unmounts, so navigating Previous/Next repeatedly
 * doesn't leak one object URL per invoice viewed in the session.
 */
export function useInvoiceDetail(invoiceId: string | undefined): InvoiceDetailState {
  const { user } = useAuth();
  const [invoice, setInvoice] = useState<InvoiceDetail | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);
  const previousBlobUrlRef = useRef<string | null>(null);

  function adoptBlobUrl(nextPdfUrl: string | null) {
    if (previousBlobUrlRef.current && previousBlobUrlRef.current !== nextPdfUrl) {
      URL.revokeObjectURL(previousBlobUrlRef.current);
    }
    previousBlobUrlRef.current = nextPdfUrl?.startsWith('blob:') ? nextPdfUrl : null;
  }

  useEffect(() => {
    if (!user || !invoiceId) {
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);
    setNotFound(false);

    invoiceDetailClient
      .getInvoiceDetail(user.tenantId, invoiceId)
      .then((result) => {
        if (cancelled) return;
        if (result) {
          adoptBlobUrl(result.pdfUrl);
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

  // Final revoke on unmount, for whichever object URL was last adopted.
  useEffect(
    () => () => {
      if (previousBlobUrlRef.current) {
        URL.revokeObjectURL(previousBlobUrlRef.current);
      }
    },
    [],
  );

  return {
    invoice,
    isLoading,
    error,
    notFound,
    retry: () => setReloadToken((t) => t + 1),
    applyUpdatedInvoice: (updated: InvoiceDetail) => {
      adoptBlobUrl(updated.pdfUrl);
      setInvoice(updated);
    },
  };
}
