import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { invoiceClient } from '@/api/invoiceClient';
import type { InvoiceQueryResult, InvoiceSortField, SortDirection } from '@/types/invoice';

const DEFAULT_PAGE_SIZE = 10;
const SEARCH_DEBOUNCE_MS = 300;

export interface InvoiceQueueState {
  search: string;
  setSearch: (value: string) => void;
  status: string | undefined;
  setStatus: (value: string | undefined) => void;
  sortBy: InvoiceSortField;
  sortDirection: SortDirection;
  toggleSort: (field: InvoiceSortField) => void;
  page: number;
  setPage: (value: number) => void;
  pageSize: number;
  result: InvoiceQueryResult | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * Owns all query state for the Invoice work queue (search text, status
 * filter, sort field/direction, current page) and re-queries the
 * InvoiceClient whenever any of it changes, exposing loading/error state
 * per WP-015 task 6.
 *
 * @param initialStatus seeds the status filter, e.g. from the
 *   `/invoices/:statusCode` route param set by WP-014's data-driven nav.
 * @param fixedStatuses (WP-074) a fixed set of statuses to combine, for the
 *   Query Queue view (NEEDS_QUERY/QUERY_RAISED/AWAITING_SUPPLIER_RESPONSE).
 *   Unlike `initialStatus`/`status`, this isn't exposed as adjustable state -
 *   there's no dropdown to narrow an already-combined view further to one of
 *   its own statuses - it's just threaded straight through to every query.
 */
export function useInvoiceQueue(initialStatus?: string, fixedStatuses?: string[]): InvoiceQueueState {
  const { user } = useAuth();
  const [search, setSearchInternal] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [status, setStatus] = useState<string | undefined>(initialStatus);
  const [sortBy, setSortBy] = useState<InvoiceSortField>('invoiceDate');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<InvoiceQueryResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // Keep the status filter in sync if the route param changes (e.g. the
  // user clicks a different Invoice Queue sub-link in the nav).
  useEffect(() => {
    setStatus(initialStatus);
    setPage(1);
  }, [initialStatus]);

  function setSearch(value: string) {
    setSearchInternal(value);
  }

  // Debounce free-text search so every keystroke doesn't trigger a query.
  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(search);
      setPage(1);
    }, SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timeout);
  }, [search]);

  function toggleSort(field: InvoiceSortField) {
    if (field === sortBy) {
      setSortDirection((previous) => (previous === 'asc' ? 'desc' : 'asc'));
    } else {
      setSortBy(field);
      setSortDirection('asc');
    }
    setPage(1);
  }

  const queryKey = useMemo(
    () => ({
      tenantId: user?.tenantId,
      search: debouncedSearch,
      status,
      statuses: fixedStatuses,
      sortBy,
      sortDirection,
      page,
      reloadToken,
    }),
    // fixedStatuses is a caller-provided fixed set for this page's lifetime
    // (see the hook's own doc comment) - a new array reference on every
    // render would otherwise re-trigger the query effect below on every
    // render, so it's deliberately joined into a stable string for the
    // dependency array rather than compared by reference.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [user?.tenantId, debouncedSearch, status, fixedStatuses?.join(','), sortBy, sortDirection, page, reloadToken],
  );

  useEffect(() => {
    if (!queryKey.tenantId) {
      setResult(null);
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    invoiceClient
      .queryInvoices({
        tenantId: queryKey.tenantId,
        search: queryKey.search,
        status: queryKey.status,
        statuses: queryKey.statuses,
        sortBy: queryKey.sortBy,
        sortDirection: queryKey.sortDirection,
        page: queryKey.page,
        pageSize: DEFAULT_PAGE_SIZE,
      })
      .then((nextResult) => {
        if (!cancelled) {
          setResult(nextResult);
          setIsLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load invoices. Please try again.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [queryKey]);

  return {
    search,
    setSearch,
    status,
    setStatus: (value: string | undefined) => {
      setStatus(value);
      setPage(1);
    },
    sortBy,
    sortDirection,
    toggleSort,
    page,
    setPage,
    pageSize: DEFAULT_PAGE_SIZE,
    result,
    isLoading,
    error,
    retry: () => setReloadToken((previous) => previous + 1),
  };
}
