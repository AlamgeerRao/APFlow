import { describe, expect, it } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useInvoiceNotes } from '@/api/useInvoiceNotes';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function wrapper({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

describe('useInvoiceNotes', () => {
  it('loads notes sorted chronologically, oldest first (task 4)', async () => {
    // inv-pd-001's fixture notes are seeded out of order (note-pd-001-2 is
    // earlier in the array but has the LATER timestamp) specifically to
    // exercise this sort.
    const { result } = renderHook(() => useInvoiceNotes('inv-pd-001'), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.notes.map((n) => n.id)).toEqual(['note-pd-001-1', 'note-pd-001-2']);
  });

  it('starts with an empty, non-error list for an invoice with no notes', async () => {
    const { result } = renderHook(() => useInvoiceNotes('inv-pd-005'), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.notes).toEqual([]);
    expect(result.current.error).toBeNull();
  });

  it('adds a note, refreshes the list, and reports success (tasks 2, 6)', async () => {
    const { result } = renderHook(() => useInvoiceNotes('inv-pd-006'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.notes).toHaveLength(0);

    const succeeded = await act(async () => result.current.addNote('A freshly added note.'));

    expect(succeeded).toBe(true);
    await waitFor(() => expect(result.current.notes).toHaveLength(1));
    expect(result.current.notes[0].content).toBe('A freshly added note.');
    expect(result.current.notes[0].authorName).toBe('Test User');
    expect(result.current.submitError).toBeNull();
  });

  it('reports failure and keeps submitError set for invalid content, without touching the list', async () => {
    const { result } = renderHook(() => useInvoiceNotes('inv-pd-007'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const succeeded = await act(async () => result.current.addNote('   '));

    expect(succeeded).toBe(false);
    expect(result.current.submitError).toMatch(/must not be empty/i);
    expect(result.current.notes).toHaveLength(0);
  });
});
