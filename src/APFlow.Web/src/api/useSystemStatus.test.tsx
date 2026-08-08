import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useSystemStatus } from '@/api/useSystemStatus';
import { systemStatusClient } from '@/api/systemStatusClient';

vi.mock('@/api/systemStatusClient', () => ({
  systemStatusClient: {
    getLiveness: vi.fn(),
    getReadiness: vi.fn(),
  },
}));

describe('useSystemStatus', () => {
  it('loads liveness and readiness together', async () => {
    vi.mocked(systemStatusClient.getLiveness).mockResolvedValue({ status: 'Healthy', checks: [] });
    vi.mocked(systemStatusClient.getReadiness).mockResolvedValue({
      status: 'Healthy',
      checks: [{ name: 'database', status: 'Healthy', description: null }],
    });

    const { result } = renderHook(() => useSystemStatus());

    expect(result.current.isLoading).toBe(true);
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.liveness).toEqual({ status: 'Healthy', checks: [] });
    expect(result.current.readiness?.checks).toHaveLength(1);
    expect(result.current.error).toBeNull();
  });

  it('surfaces an error and stops loading if either call fails', async () => {
    vi.mocked(systemStatusClient.getLiveness).mockResolvedValue({ status: 'Healthy', checks: [] });
    vi.mocked(systemStatusClient.getReadiness).mockRejectedValue(new Error('network error'));

    const { result } = renderHook(() => useSystemStatus());

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toBe('Unable to load system status. Please try again.');
  });

  it('retry re-queries both endpoints', async () => {
    vi.mocked(systemStatusClient.getLiveness).mockResolvedValue({ status: 'Healthy', checks: [] });
    vi.mocked(systemStatusClient.getReadiness).mockResolvedValue({ status: 'Healthy', checks: [] });

    const { result } = renderHook(() => useSystemStatus());
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const callsBefore = vi.mocked(systemStatusClient.getLiveness).mock.calls.length;
    act(() => result.current.retry());

    await waitFor(() =>
      expect(vi.mocked(systemStatusClient.getLiveness).mock.calls.length).toBeGreaterThan(callsBefore),
    );
  });
});
