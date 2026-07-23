import { useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { invoiceClient } from '@/api/invoiceClient';

interface InvoiceNavigationState {
  previousId: string | null;
  nextId: string | null;
  position: number | null;
  total: number | null;
}

/**
 * Computes the Previous/Next invoice ids for the Review Screen (WP-016
 * task 6), by traversing the tenant's full non-terminal invoice list in
 * the same default order as the unfiltered Invoice Queue (invoiceDate
 * descending — see WP-015's useInvoiceQueue default).
 *
 * DECISION: traversal always uses this tenant-wide default order,
 * regardless of which search/filter/sort the user had applied on the
 * queue page they arrived from. See
 * docs/WP-016-Invoice-Review-Decisions.md for why, and what preserving
 * the originating queue's exact order would require.
 */
export function useInvoiceNavigation(currentInvoiceId: string | undefined): InvoiceNavigationState {
  const { user } = useAuth();
  const [orderedIds, setOrderedIds] = useState<string[]>([]);

  useEffect(() => {
    if (!user) return;
    let cancelled = false;

    invoiceClient
      .queryInvoices({
        tenantId: user.tenantId,
        sortBy: 'invoiceDate',
        sortDirection: 'desc',
        page: 1,
        pageSize: 1000,
      })
      .then((result) => {
        if (!cancelled) {
          setOrderedIds(result.items.map((item) => item.id));
        }
      })
      .catch(() => {
        if (!cancelled) setOrderedIds([]);
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  if (!currentInvoiceId || orderedIds.length === 0) {
    return { previousId: null, nextId: null, position: null, total: null };
  }

  const index = orderedIds.indexOf(currentInvoiceId);
  if (index === -1) {
    return { previousId: null, nextId: null, position: null, total: orderedIds.length };
  }

  return {
    previousId: index > 0 ? orderedIds[index - 1] : null,
    nextId: index < orderedIds.length - 1 ? orderedIds[index + 1] : null,
    position: index + 1,
    total: orderedIds.length,
  };
}
