import type { SystemHealthStatus } from '@/types/systemStatus';

interface HealthStatusBadgeProps {
  status: SystemHealthStatus;
}

const STATUS_CLASSES: Record<SystemHealthStatus, string> = {
  Healthy: 'bg-emerald-100 text-emerald-800',
  Degraded: 'bg-amber-100 text-amber-800',
  Unhealthy: 'bg-red-100 text-red-800',
};

/** Renders a health status value with a consistent colour per state (WP-043). */
export function HealthStatusBadge({ status }: HealthStatusBadgeProps) {
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_CLASSES[status]}`}>
      {status}
    </span>
  );
}
