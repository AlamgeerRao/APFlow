import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { supplierFolderClient } from '@/api/supplierFolderClient';
import { dashboardClient } from '@/api/dashboardClient';
import type { FolderSummary } from '@/types/supplierFolder';
import type { RecentActivityItem } from '@/types/dashboard';

const RECENT_ACTIVITY_LIMIT = 8;

export interface DashboardState {
  folderCounts: FolderSummary[];
  recentActivity: RecentActivityItem[];
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * Owns the Dashboard's (WP-030) data: per-status invoice counts (reusing
 * `supplierFolderClient.getFolderCounts`, WP-059 — no new endpoint) and the
 * most recently created invoices (`dashboardClient.getRecentActivity`,
 * reusing `GET /api/invoices` sorted by `CreatedAtUtc`). Both are fetched
 * together on mount and whenever the acting tenant changes, following the
 * same `Promise.all` + cancellation-flag pattern `useSupplierFolderView`
 * already established.
 */
export function useDashboard(): DashboardState {
  const { user } = useAuth();

  const [folderCounts, setFolderCounts] = useState<FolderSummary[]>([]);
  const [recentActivity, setRecentActivity] = useState<RecentActivityItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const queryKey = useMemo(() => ({ tenantId: user?.tenantId, reloadToken }), [user?.tenantId, reloadToken]);

  useEffect(() => {
    if (!queryKey.tenantId) {
      setFolderCounts([]);
      setRecentActivity([]);
      setIsLoading(false);
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    Promise.all([
      supplierFolderClient.getFolderCounts(queryKey.tenantId),
      dashboardClient.getRecentActivity(queryKey.tenantId, RECENT_ACTIVITY_LIMIT),
    ])
      .then(([counts, activity]) => {
        if (cancelled) return;
        setFolderCounts(counts);
        setRecentActivity(activity);
        setIsLoading(false);
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load dashboard data. Please try again.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [queryKey]);

  return {
    folderCounts,
    recentActivity,
    isLoading,
    error,
    retry: () => setReloadToken((previous) => previous + 1),
  };
}
