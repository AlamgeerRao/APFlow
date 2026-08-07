import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SuppliersWithoutInvoices } from '@/components/supplierFolder/SuppliersWithoutInvoices';
import type { Supplier } from '@/types/supplier';

const brandNewSupplier: Supplier = {
  id: 'supplier-002',
  name: 'Brand New Supplier',
  code: null,
  email: null,
  phone: null,
  creditLimit: null,
  paymentTermsDays: null,
  accountingReference: null,
  status: 'ACTIVE',
  createdAtUtc: '2026-08-07T09:00:00Z',
};

describe('SuppliersWithoutInvoices (WP-093)', () => {
  it('renders nothing when there are no zero-invoice suppliers', () => {
    const { container } = render(<SuppliersWithoutInvoices suppliers={[]} onEdit={vi.fn()} />);

    expect(container).toBeEmptyDOMElement();
  });

  it('lists each supplier with an Edit action, under a heading naming the section', () => {
    render(<SuppliersWithoutInvoices suppliers={[brandNewSupplier]} onEdit={vi.fn()} />);

    expect(screen.getByRole('heading', { name: 'Suppliers with no invoices yet' })).toBeInTheDocument();
    expect(screen.getByText('Brand New Supplier')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
  });

  it('calls onEdit with the supplier when its Edit action is clicked', async () => {
    const user = userEvent.setup();
    const onEdit = vi.fn();
    render(<SuppliersWithoutInvoices suppliers={[brandNewSupplier]} onEdit={onEdit} />);

    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(onEdit).toHaveBeenCalledWith(brandNewSupplier);
  });
});
