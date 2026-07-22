import { PageHeading } from '@/components/layout/PageHeading';

/** Placeholder route content. Approved business functionality is out of scope for WP-014. */
export function ApprovedPage() {
  return (
    <>
      <PageHeading title="Approved" description="Invoices approved and ready for payment processing." />
      <p className="text-sm text-slate-600">Approved content will be implemented in WP-018.</p>
    </>
  );
}
