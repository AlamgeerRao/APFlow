import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { ExtractedFieldsPanel } from '@/components/invoiceReview/ExtractedFieldsPanel';

describe('ExtractedFieldsPanel', () => {
  it('renders each field with its label, value, and confidence score', () => {
    render(
      <ExtractedFieldsPanel
        fields={[
          { fieldKey: 'supplierName', label: 'Supplier Name', value: 'Northwind Traders Ltd', confidenceScore: 0.98 },
          { fieldKey: 'vatNumber', label: 'VAT Number', value: 'Not detected', confidenceScore: 0.31 },
        ]}
      />,
    );

    expect(screen.getByText('Supplier Name')).toBeInTheDocument();
    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('98%')).toBeInTheDocument();

    expect(screen.getByText('VAT Number')).toBeInTheDocument();
    expect(screen.getByText('Not detected')).toBeInTheDocument();
    expect(screen.getByText('31%')).toBeInTheDocument();
  });

  it('shows an empty-state message when there are no extracted fields', () => {
    render(<ExtractedFieldsPanel fields={[]} />);

    expect(screen.getByText(/No extracted fields available/i)).toBeInTheDocument();
  });
});
