import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useInvoiceNotes } from '@/api/useInvoiceNotes';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient, ApiError } from '@/api/httpClient';

// WP-020: content validation now happens server-side (WP-055); this suite
// mocks httpClient directly and tests the hook's own plumbing (loading,
// chronological sort, add-then-refresh, success/error handling).
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), post: vi.fn() } };
});

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function wrapper({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.post).mockReset();
});

describe('useInvoiceNotes', () => {
  it('loads notes sorted chronologically, oldest first (task 4)', async () => {
    // Seeded out of order deliberately, to exercise the hook's own sort:
    // note-2 comes first in the mocked response but has the LATER timestamp.
    vi.mocked(httpClient.get).mockResolvedValueOnce([
      { id: 'note-2', content: 'Second note', authorDisplayName: 'Test User', createdAtUtc: '2026-07-02T09:00:00Z' },
      { id: 'note-1', content: 'First note', authorDisplayName: 'Test User', createdAtUtc: '2026-07-01T09:00:00Z' },
    ]);

    const { result } = renderHook(() => useInvoiceNotes('inv-pd-001'), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/inv-pd-001/notes');
    expect(result.current.notes.map((n) => n.id)).toEqual(['note-1', 'note-2']);
  });

  it('starts with an empty, non-error list for an invoice with no notes', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);

    const { result } = renderHook(() => useInvoiceNotes('inv-pd-005'), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.notes).toEqual([]);
    expect(result.current.error).toBeNull();
  });

  it('adds a note, refreshes the list, and reports success (tasks 2, 6)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]).mockResolvedValueOnce([
      { id: 'note-new', content: 'A freshly added note.', authorDisplayName: 'Test User', createdAtUtc: '2026-07-10T09:00:00Z' },
    ]);
    vi.mocked(httpClient.post).mockResolvedValueOnce({
      id: 'note-new',
      content: 'A freshly added note.',
      authorDisplayName: 'Test User',
      createdAtUtc: '2026-07-10T09:00:00Z',
    });

    const { result } = renderHook(() => useInvoiceNotes('inv-pd-006'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.notes).toHaveLength(0);

    const succeeded = await act(async () => result.current.addNote('A freshly added note.'));

    expect(succeeded).toBe(true);
    expect(httpClient.post).toHaveBeenCalledWith('/api/invoices/inv-pd-006/notes', {
      content: 'A freshly added note.',
    });
    await waitFor(() => expect(result.current.notes).toHaveLength(1));
    expect(result.current.notes[0].content).toBe('A freshly added note.');
    expect(result.current.submitError).toBeNull();
  });

  it('reports failure and keeps submitError set when the server rejects invalid content, without touching the list', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    vi.mocked(httpClient.post).mockRejectedValueOnce(
      new ApiError(400, 'Note content must not be empty.', 'Note.ContentRequired'),
    );

    const { result } = renderHook(() => useInvoiceNotes('inv-pd-007'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const succeeded = await act(async () => result.current.addNote('   '));

    expect(succeeded).toBe(false);
    expect(result.current.submitError).toMatch(/must not be empty/i);
    expect(result.current.notes).toHaveLength(0);
  });
});
