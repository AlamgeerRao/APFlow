import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { NotesPanel } from '@/components/invoiceReview/NotesPanel';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

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

describe('NotesPanel', () => {
  it('loads and displays existing notes for an invoice (task 1)', async () => {
    renderPanel('inv-gb-001');

    await waitFor(() => expect(screen.queryByText(/loading notes/i)).not.toBeInTheDocument());

    expect(screen.getByText(/Approved for payment/i)).toBeInTheDocument();
    expect(screen.getByText(/Patrick/)).toBeInTheDocument();
  });

  it('adds a note end-to-end and shows it in the list afterwards, in chronological order (tasks 2, 4, 6)', async () => {
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
    renderPanel('inv-gb-001');
    await waitFor(() => expect(screen.queryByText(/loading notes/i)).not.toBeInTheDocument());

    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });
});
