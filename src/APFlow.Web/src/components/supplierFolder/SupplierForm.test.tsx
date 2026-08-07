import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SupplierForm } from '@/components/supplierFolder/SupplierForm';
import type { Supplier } from '@/types/supplier';

const supplier: Supplier = {
  id: 'supplier-001',
  name: 'Northwind Traders Ltd',
  code: 'NORTH01',
  email: 'accounts@northwindtraders.example',
  phone: '+44 20 7946 0001',
  creditLimit: 15000,
  paymentTermsDays: 30,
  accountingReference: 'NORTHW',
  status: 'ACTIVE',
  createdAtUtc: '2026-01-10T09:00:00Z',
};

function renderForm(overrides: Partial<React.ComponentProps<typeof SupplierForm>> = {}) {
  const onSubmit = vi.fn().mockResolvedValue(supplier);
  const onCancel = vi.fn();
  render(
    <SupplierForm
      isFinanceManager={false}
      onSubmit={onSubmit}
      onCancel={onCancel}
      isSubmitting={false}
      submitError={null}
      submitErrorIsCreditLimitForbidden={false}
      {...overrides}
    />,
  );
  return { onSubmit, onCancel };
}

describe('SupplierForm — Credit Limit permission split (WP-027 task 3)', () => {
  it('a non-FINANCE_MANAGER caller sees Credit Limit as plain read-only text, not a disabled input', () => {
    renderForm({ supplier, isFinanceManager: false });

    // Visible — never hidden.
    expect(screen.getByText('Credit limit')).toBeInTheDocument();
    expect(screen.getByTestId('credit-limit-readonly')).toHaveTextContent('15,000.00');

    // Not rendered as any kind of <input>/<textarea> at all — specifically
    // not a disabled/greyed-out one, which the WP brief rules out by name.
    expect(screen.queryByLabelText('Credit limit')).not.toBeInTheDocument();
    const numberInputs = screen.queryAllByRole('spinbutton', { name: /credit limit/i });
    expect(numberInputs).toHaveLength(0);
  });

  it('a FINANCE_MANAGER caller sees Credit Limit as a normal editable input', async () => {
    const user = userEvent.setup();
    renderForm({ supplier, isFinanceManager: true });

    const input = screen.getByLabelText('Credit limit');
    expect(input).not.toBeDisabled();
    expect(input).toHaveValue(15000);

    await user.clear(input);
    await user.type(input, '20000');
    expect(input).toHaveValue(20000);
  });

  it('a supplier with no credit limit set shows "Not set" for a non-FINANCE_MANAGER caller', () => {
    renderForm({ supplier: { ...supplier, creditLimit: null }, isFinanceManager: false });

    expect(screen.getByTestId('credit-limit-readonly')).toHaveTextContent('Not set');
  });

  it('a non-FINANCE_MANAGER caller submitting other field edits round-trips the existing credit limit unchanged', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm({ supplier, isFinanceManager: false });

    await user.clear(screen.getByLabelText('Phone'));
    await user.type(screen.getByLabelText('Phone'), '+44 20 0000 0000');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ creditLimit: 15000, phone: '+44 20 0000 0000' }));
  });

  it('a non-FINANCE_MANAGER caller creating a new supplier submits a null credit limit (nothing to round-trip yet)', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm({ isFinanceManager: false });

    await user.type(screen.getByLabelText('Name'), 'Brand New Supplier');
    await user.click(screen.getByRole('button', { name: 'Add supplier' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ creditLimit: null, name: 'Brand New Supplier' }));
  });

  it('a FINANCE_MANAGER caller can submit a changed credit limit', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm({ supplier, isFinanceManager: true });

    await user.clear(screen.getByLabelText('Credit limit'));
    await user.type(screen.getByLabelText('Credit limit'), '25000');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({ creditLimit: 25000 }));
  });
});

describe('SupplierForm — general fields and validation', () => {
  it('renders "Add supplier" in create mode with every field editable and empty', () => {
    renderForm({ isFinanceManager: true });

    expect(screen.getByRole('heading', { name: 'Add supplier' })).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Add supplier' })).toBeInTheDocument();
  });

  it('renders "Edit {name}" in edit mode, pre-filled with the supplier\'s current values', () => {
    renderForm({ supplier, isFinanceManager: true });

    expect(screen.getByRole('heading', { name: 'Edit Northwind Traders Ltd' })).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveValue('Northwind Traders Ltd');
    expect(screen.getByLabelText('Code')).toHaveValue('NORTH01');
    expect(screen.getByLabelText('Payment terms (days)')).toHaveValue(30);
  });

  it('rejects an empty name and does not call onSubmit', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm({ isFinanceManager: false });

    await user.click(screen.getByRole('button', { name: 'Add supplier' }));

    expect(screen.getByRole('alert')).toHaveTextContent(/Enter a supplier name/);
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('rejects a negative payment terms value', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm({ isFinanceManager: false });

    await user.type(screen.getByLabelText('Name'), 'Valid Name');
    await user.type(screen.getByLabelText('Payment terms (days)'), '-5');
    await user.click(screen.getByRole('button', { name: 'Add supplier' }));

    expect(screen.getByText(/Payment terms \(days\) must be a non-negative whole number/)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('submits empty optional fields as null, not empty strings', async () => {
    const user = userEvent.setup();
    const { onSubmit } = renderForm({ isFinanceManager: false });

    await user.type(screen.getByLabelText('Name'), 'Minimal Supplier');
    await user.click(screen.getByRole('button', { name: 'Add supplier' }));

    expect(onSubmit).toHaveBeenCalledWith({
      name: 'Minimal Supplier',
      code: null,
      email: null,
      phone: null,
      creditLimit: null,
      paymentTermsDays: null,
      accountingReference: null,
      status: 'ACTIVE',
    });
  });

  it('shows the submit error banner from a failed submission, with a targeted message for a 403 CreditLimitForbidden', () => {
    renderForm({
      supplier,
      isFinanceManager: false,
      submitError: "Changing a supplier's credit limit requires the 'FINANCE_MANAGER' role.",
      submitErrorIsCreditLimitForbidden: true,
    });

    expect(screen.getByText(/You don't have permission to change the credit limit/)).toBeInTheDocument();
  });

  it('calls onCancel when Cancel is clicked', async () => {
    const user = userEvent.setup();
    const { onCancel } = renderForm({ isFinanceManager: false });

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onCancel).toHaveBeenCalled();
  });

  it('disables all controls and shows "Saving..." while isSubmitting', () => {
    renderForm({ isFinanceManager: true, isSubmitting: true });

    expect(screen.getByRole('button', { name: 'Saving...' })).toBeDisabled();
    expect(screen.getByLabelText('Name')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
  });
});
