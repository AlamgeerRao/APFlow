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
 */
export function useInvoiceQueue(initialStatus?: string): InvoiceQueueState {
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
  // user clicks a different Invoice Queue sub-link in the nav). Adjusting
  // state in response to a prop change during render (React docs: "You
  // Might Not Need an Effect") rather than in a useEffect, since this is
  // the "reset derived state when a prop changes" case, not a
  // synchronization-with-an-external-system case.
  const [prevInitialStatus, setPrevInitialStatus] = useState(initialStatus);
  if (initialStatus !== prevInitialStatus) {
    setPrevInitialStatus(initialStatus);
    setStatus(initialStatus);
    setPage(1);
  }

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
      sortBy,
      sortDirection,
      page,
      reloadToken,
    }),
    [user?.tenantId, debouncedSearch, status, sortBy, sortDirection, page, reloadToken],
  );

  useEffect(() => {
    if (!queryKey.tenantId) {
      // No tenant to query yet - the hook's return value below overrides
      // result/isLoading/error directly rather than this effect resetting
      // them, so there is nothing to synchronize here.
      return;
    }

    let cancelled = false;
    // Standard cancellable-fetch pattern (React docs: "You Might Not Need an
    // Effect"): resetting isLoading/error before the async call is the effect
    // synchronizing with the external API, not derivable during render. Same
    // justification as useWorkflowTemplate.ts's identical case.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);

    invoiceClient
      .queryInvoices({
        tenantId: queryKey.tenantId,
        search: queryKey.search,
        status: queryKey.status,
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

  const noTenant = !queryKey.tenantId;

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
    result: noTenant ? null : result,
    isLoading: noTenant ? false : isLoading,
    error: noTenant ? null : error,
    retry: () => setReloadToken((previous) => previous + 1),
  };
}
