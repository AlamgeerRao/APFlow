import { useEffect, useState } from 'react';
import { systemStatusClient } from '@/api/systemStatusClient';
import type { HealthReport } from '@/types/systemStatus';

export interface SystemStatusState {
  liveness: HealthReport | null;
  readiness: HealthReport | null;
  isLoading: boolean;
  error: string | null;
  retry: () => void;
}

/**
 * Owns WP-043's System Status page data: liveness and readiness, fetched
 * together on mount. Unlike every other data hook in this codebase, this one
 * has no tenant dependency at all — health status is infrastructure-level,
 * not tenant-scoped application data, so there's nothing to gate the fetch
 * on and no `user` to read from `useAuth`.
 */
export function useSystemStatus(): SystemStatusState {
  const [liveness, setLiveness] = useState<HealthReport | null>(null);
  const [readiness, setReadiness] = useState<HealthReport | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setIsLoading(true);
    setError(null);

    Promise.all([systemStatusClient.getLiveness(), systemStatusClient.getReadiness()])
      .then(([livenessReport, readinessReport]) => {
        if (cancelled) return;
        setLiveness(livenessReport);
        setReadiness(readinessReport);
        setIsLoading(false);
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load system status. Please try again.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [reloadToken]);

  return {
    liveness,
    readiness,
    isLoading,
    error,
    retry: () => setReloadToken((previous) => previous + 1),
  };
}
