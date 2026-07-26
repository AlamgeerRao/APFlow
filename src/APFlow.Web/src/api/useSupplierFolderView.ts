import { useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useAuth } from '@/auth/useAuth';
import { supplierFolderClient } from '@/api/supplierFolderClient';
import type { FolderSummary, SupplierFolderQueryResult } from '@/types/supplierFolder';

const DEFAULT_PAGE_SIZE = 5;
const SEARCH_DEBOUNCE_MS = 300;

export interface SupplierFolderViewState {
  search: string;
  setSearch: (value: string) => void;
  folder: string | undefined;
  setFolder: (value: string | undefined) => void;
  supplier: string | undefined;
  setSupplier: (value: string | undefined) => void;
  page: number;
  setPage: (value: number) => void;
  pageSize: number;
  folderCounts: FolderSummary[];
  supplierOptions: string[];
  result: SupplierFolderQueryResult | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * Owns all query state for the Supplier & Folder Views page (WP-019) —
 * folder, supplier filter, search text, and page — and re-queries
 * `supplierFolderClient` whenever any of it changes.
 *
 * DECISION: filters are synced to the URL's query string (`?folder=...
 * &supplier=...&search=...&page=...`) rather than to localStorage, so
 * "remember selected filters" (task 6) means the browser back/forward
 * buttons and a page refresh both preserve them, and a filtered view can
 * be bookmarked/shared as a link. See
 * docs/WP-019-Supplier-Folder-Views-Decisions.md for the alternative
 * considered (persisting across sessions even after closing the tab) and
 * why this was chosen as the default.
 */
export function useSupplierFolderView(): SupplierFolderViewState {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();

  const folder = searchParams.get('folder') ?? undefined;
  const supplier = searchParams.get('supplier') ?? undefined;
  const urlSearch = searchParams.get('search') ?? '';
  const page = Number(searchParams.get('page') ?? '1') || 1;

  const [searchInput, setSearchInput] = useState(urlSearch);
  const [debouncedSearch, setDebouncedSearch] = useState(urlSearch);

  const [folderCounts, setFolderCounts] = useState<FolderSummary[]>([]);
  const [supplierOptions, setSupplierOptions] = useState<string[]>([]);
  const [result, setResult] = useState<SupplierFolderQueryResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  function updateParams(next: { folder?: string; supplier?: string; search?: string; page?: number }) {
    const merged = new URLSearchParams(searchParams);
    const entries: [string, string | number | undefined][] = [
      ['folder', next.folder !== undefined ? next.folder : folder],
      ['supplier', next.supplier !== undefined ? next.supplier : supplier],
      ['search', next.search !== undefined ? next.search : urlSearch],
      ['page', next.page ?? page],
    ];
    for (const [key, value] of entries) {
      if (value === undefined || value === '' || value === 1) {
        merged.delete(key);
      } else {
        merged.set(key, String(value));
      }
    }
    setSearchParams(merged, { replace: true });
  }

  function setFolder(value: string | undefined) {
    updateParams({ folder: value, page: 1 });
  }

  function setSupplier(value: string | undefined) {
    updateParams({ supplier: value, page: 1 });
  }

  function setSearch(value: string) {
    setSearchInput(value);
  }

  function setPage(value: number) {
    updateParams({ page: value });
  }

  // Debounce free-text search before it reaches the URL/query.
  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(searchInput);
      updateParams({ search: searchInput, page: 1 });
    }, SEARCH_DEBOUNCE_MS);
    return () => window.clearTimeout(timeout);
    // updateParams intentionally omitted: it closes over current URL state
    // and would cause this effect to re-run on every navigation, defeating
    // the debounce.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  const queryKey = useMemo(
    () => ({ tenantId: user?.tenantId, folder, supplier, search: debouncedSearch, page, reloadToken }),
    [user?.tenantId, folder, supplier, debouncedSearch, page, reloadToken],
  );

  useEffect(() => {
    if (!queryKey.tenantId) {
      // No tenant to query yet - the hook's return value below overrides
      // result/folderCounts/supplierOptions/isLoading directly rather than
      // this effect resetting them, so there is nothing to synchronize here.
      return;
    }

    let cancelled = false;
    // Standard cancellable-fetch pattern (React docs: "You Might Not Need an
    // Effect"): resetting isLoading/error before the async call is the effect
    // synchronizing with the external API, not derivable during render. Same
    // justification as useInvoiceQueue.ts's identical case.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);

    Promise.all([
      supplierFolderClient.getFolderCounts(queryKey.tenantId, queryKey.search),
      supplierFolderClient.getSuppliers(queryKey.tenantId, queryKey.folder, queryKey.search),
      supplierFolderClient.getGroupedInvoices({
        tenantId: queryKey.tenantId,
        folder: queryKey.folder,
        supplier: queryKey.supplier,
        search: queryKey.search,
        page: queryKey.page,
        pageSize: DEFAULT_PAGE_SIZE,
      }),
    ])
      .then(([counts, suppliers, groupedResult]) => {
        if (cancelled) return;
        setFolderCounts(counts);
        setSupplierOptions(suppliers);
        setResult(groupedResult);
        setIsLoading(false);
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load suppliers and invoices. Please try again.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [queryKey]);

  const noTenant = !queryKey.tenantId;

  return {
    search: searchInput,
    setSearch,
    folder,
    setFolder,
    supplier,
    setSupplier,
    page,
    setPage,
    pageSize: DEFAULT_PAGE_SIZE,
    folderCounts: noTenant ? [] : folderCounts,
    supplierOptions: noTenant ? [] : supplierOptions,
    result: noTenant ? null : result,
    isLoading: noTenant ? false : isLoading,
    error: noTenant ? null : error,
    retry: () => setReloadToken((t) => t + 1),
  };
}
