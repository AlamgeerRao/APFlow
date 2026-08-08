import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { SystemStatusPage } from '@/pages/SystemStatusPage';
import { systemStatusClient } from '@/api/systemStatusClient';

vi.mock('@/api/systemStatusClient', () => ({
  systemStatusClient: {
    getLiveness: vi.fn(),
    getReadiness: vi.fn(),
  },
}));

describe('SystemStatusPage', () => {
  it('shows a loading state, then both health cards and the build info', async () => {
    vi.mocked(systemStatusClient.getLiveness).mockResolvedValue({ status: 'Healthy', checks: [] });
    vi.mocked(systemStatusClient.getReadiness).mockResolvedValue({
      status: 'Healthy',
      checks: [
        { name: 'database', status: 'Healthy', description: null },
        { name: 'graph-mailbox', status: 'Healthy', description: null },
        { name: 'blob-storage', status: 'Healthy', description: null },
      ],
    });

    render(<SystemStatusPage />);

    expect(screen.getByRole('status')).toBeInTheDocument();

    await waitFor(() => expect(screen.getByText('Liveness')).toBeInTheDocument());

    expect(screen.getByText('Readiness')).toBeInTheDocument();
    expect(screen.getByText('database')).toBeInTheDocument();
    expect(screen.getByText('graph-mailbox')).toBeInTheDocument();
    expect(screen.getByText('blob-storage')).toBeInTheDocument();
    expect(screen.getByText('Build')).toBeInTheDocument();
    expect(screen.getByTestId('build-sha')).toBeInTheDocument();
  });

  it('shows an error state with a working retry on failure', async () => {
    vi.mocked(systemStatusClient.getLiveness).mockRejectedValue(new Error('network error'));
    vi.mocked(systemStatusClient.getReadiness).mockRejectedValue(new Error('network error'));

    render(<SystemStatusPage />);

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());

    vi.mocked(systemStatusClient.getLiveness).mockResolvedValue({ status: 'Healthy', checks: [] });
    vi.mocked(systemStatusClient.getReadiness).mockResolvedValue({ status: 'Healthy', checks: [] });

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(screen.getByText('Liveness')).toBeInTheDocument());
  });
});
