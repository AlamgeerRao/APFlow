import { confidenceLevel, formatConfidence } from '@/utils/confidence';

interface ConfidenceBadgeProps {
  score: number;
  label?: string;
}

const LEVEL_CLASSES: Record<string, string> = {
  high: 'bg-green-100 text-green-800',
  medium: 'bg-amber-100 text-amber-800',
  low: 'bg-red-100 text-red-800',
};

const LEVEL_TEXT: Record<string, string> = {
  high: 'High confidence',
  medium: 'Medium confidence',
  low: 'Low confidence — recommend manual verification',
};

/** Colour-coded confidence score badge, used for both the overall score and per-field scores. */
export function ConfidenceBadge({ score, label }: ConfidenceBadgeProps) {
  const level = confidenceLevel(score);

  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${LEVEL_CLASSES[level]}`}
      title={LEVEL_TEXT[level]}
    >
      {label ? `${label}: ` : ''}
      {formatConfidence(score)}
      <span className="sr-only"> ({LEVEL_TEXT[level]})</span>
    </span>
  );
}
