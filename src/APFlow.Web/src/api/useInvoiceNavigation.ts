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

    // WP-072 follow-up: this used to request pageSize: 1000 in one call, which
    // real live traffic showed failing with a 400 - the real backend caps
    // InvoiceQueryParameters.PageSize at 100 (InvoiceQueryParameters.MaxPageSize).
    // The fixture client never enforced that cap, so this was never exercised
    // against the real contract before. Loops across pages instead (same
    // pattern as the backend's own SupplierFolderQueryService.
    // FetchAllMatchingInvoicesAsync), so this keeps working once a tenant has
    // more than one page of invoices, rather than just raising the constant
    // and hitting the same wall again later.
    async function fetchAllIds(tenantId: string): Promise<string[]> {
      const ids: string[] = [];
      let page = 1;
      for (;;) {
        const result = await invoiceClient.queryInvoices({
          tenantId,
          sortBy: 'invoiceDate',
          sortDirection: 'desc',
          page,
          pageSize: 100,
        });
        ids.push(...result.items.map((item) => item.id));
        if (result.items.length === 0 || ids.length >= result.totalCount) break;
        page += 1;
      }
      return ids;
    }

    fetchAllIds(user.tenantId)
      .then((ids) => {
        if (!cancelled) setOrderedIds(ids);
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
