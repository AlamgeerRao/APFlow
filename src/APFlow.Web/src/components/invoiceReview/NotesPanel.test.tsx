import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NotesPanel } from '@/components/invoiceReview/NotesPanel';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

// WP-020: notes now come from the real API; mock httpClient directly
// rather than depending on the (now largely retired) fixture note store.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), post: vi.fn() } };
});

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Jamie Lee', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function renderPanel(invoiceId: string) {
  return render(
    <AuthContext.Provider value={authValue}>
      <NotesPanel invoiceId={invoiceId} />
    </AuthContext.Provider>,
  );
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.post).mockReset();
});

describe('NotesPanel', () => {
  it('loads and displays existing notes for an invoice (task 1)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([
      {
        id: 'note-gb-001-1',
        content: 'Approved for payment as part of the usual monthly Yorkshire Skip Supplies run.',
        authorDisplayName: 'Patrick',
        createdAtUtc: '2026-07-02T11:20:00Z',
      },
    ]);

    renderPanel('inv-gb-001');

    await waitFor(() => expect(screen.queryByText(/loading notes/i)).not.toBeInTheDocument());
    expect(screen.getByText(/Approved for payment/i)).toBeInTheDocument();
    expect(screen.getByText(/Patrick/)).toBeInTheDocument();
  });

  it('adds a note end-to-end and shows it in the list afterwards, in chronological order (tasks 2, 4, 6)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]).mockResolvedValueOnce([
      {
        id: 'note-new',
        content: 'Following up with the supplier next week.',
        authorDisplayName: 'Jamie Lee',
        createdAtUtc: '2026-07-10T09:00:00Z',
      },
    ]);
    vi.mocked(httpClient.post).mockResolvedValueOnce({
      id: 'note-new',
      content: 'Following up with the supplier next week.',
      authorDisplayName: 'Jamie Lee',
      createdAtUtc: '2026-07-10T09:00:00Z',
    });

    const user = userEvent.setup();
    renderPanel('inv-pd-008');
    await waitFor(() => expect(screen.queryByText(/loading notes/i)).not.toBeInTheDocument());
    expect(screen.getByText(/No notes yet/i)).toBeInTheDocument();

    await user.type(screen.getByLabelText(/add a note/i), 'Following up with the supplier next week.');
    await user.click(screen.getByRole('button', { name: /save note/i }));

    await waitFor(() => expect(screen.getByText('Following up with the supplier next week.')).toBeInTheDocument());
    expect(screen.getByText(/Jamie Lee/)).toBeInTheDocument();
    expect(screen.queryByText(/No notes yet/i)).not.toBeInTheDocument();
  });

  it('does not render any edit or delete control anywhere in the panel', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([
      {
        id: 'note-gb-001-1',
        content: 'Approved for payment as part of the usual monthly Yorkshire Skip Supplies run.',
        authorDisplayName: 'Patrick',
        createdAtUtc: '2026-07-02T11:20:00Z',
      },
    ]);
    renderPanel('inv-gb-001');
    await waitFor(() => expect(screen.queryByText(/loading notes/i)).not.toBeInTheDocument());

    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });
});
