import { describe, expect, it } from 'vitest';
import { confidenceLevel, formatConfidence } from '@/utils/confidence';

describe('confidenceLevel', () => {
  it('classifies scores at or above 0.85 as high', () => {
    expect(confidenceLevel(0.85)).toBe('high');
    expect(confidenceLevel(0.99)).toBe('high');
  });

  it('classifies scores from 0.6 up to (but excluding) 0.85 as medium', () => {
    expect(confidenceLevel(0.6)).toBe('medium');
    expect(confidenceLevel(0.84)).toBe('medium');
  });

  it('classifies scores below 0.6 as low', () => {
    expect(confidenceLevel(0.59)).toBe('low');
    expect(confidenceLevel(0)).toBe('low');
  });
});

describe('formatConfidence', () => {
  it('formats a 0-1 score as a rounded percentage', () => {
    expect(formatConfidence(0.965)).toBe('97%');
    expect(formatConfidence(0.6)).toBe('60%');
    expect(formatConfidence(1)).toBe('100%');
  });
});
