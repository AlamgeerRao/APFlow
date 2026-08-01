import { useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { ingestionIssueClient } from '@/api/ingestionIssueClient';
import type { IngestionIssueQueryResult } from '@/types/ingestionIssue';

const DEFAULT_PAGE_SIZE = 25;

export interface IngestionIssuesState {
  page: number;
  setPage: (value: number) => void;
  pageSize: number;
  result: IngestionIssueQueryResult | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/** Owns the Inbox's paging state and re-queries IngestionIssueClient whenever the page changes, exposing loading/error state (same shape as useInvoiceQueue). */
export function useIngestionIssues(): IngestionIssuesState {
  const { user } = useAuth();
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<IngestionIssueQueryResult | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    if (!user) {
      setResult(null);
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    ingestionIssueClient
      .queryIngestionIssues(page, DEFAULT_PAGE_SIZE)
      .then((nextResult) => {
        if (!cancelled) {
          setResult(nextResult);
          setIsLoading(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load the Inbox. Please try again.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [user, page, reloadToken]);

  return {
    page,
    setPage,
    pageSize: DEFAULT_PAGE_SIZE,
    result,
    isLoading,
    error,
    retry: () => setReloadToken((previous) => previous + 1),
  };
}
