/** Aggregate or per-check health status, matching ASP.NET Core's `HealthStatus` enum names exactly. */
export type SystemHealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy';

/** One dependency check's result (`GET /health/ready`'s `checks` array). */
export interface HealthCheckEntry {
  name: string;
  status: SystemHealthStatus;
  description: string | null;
}

/** Response shape shared by `GET /health/live` and `GET /health/ready` (WP-043). */
export interface HealthReport {
  status: SystemHealthStatus;
  checks: HealthCheckEntry[];
}
