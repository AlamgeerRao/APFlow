export type ConfidenceLevel = 'high' | 'medium' | 'low';

/**
 * Thresholds for categorising a 0–1 Document Intelligence confidence
 * score for display. Not a backend-confirmed business rule — a reasoned
 * UI default, flagged in docs/WP-016-Invoice-Review-Decisions.md.
 */
export function confidenceLevel(score: number): ConfidenceLevel {
  if (score >= 0.85) return 'high';
  if (score >= 0.6) return 'medium';
  return 'low';
}

export function formatConfidence(score: number): string {
  return `${Math.round(score * 100)}%`;
}
