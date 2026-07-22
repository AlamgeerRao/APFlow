import { PageHeading } from '@/components/layout/PageHeading';

/** Placeholder route content. Query Queue business functionality is out of scope for WP-014. */
export function QueryQueuePage() {
  return (
    <>
      <PageHeading title="Query Queue" description="Invoices with an open query to a supplier." />
      <p className="text-sm text-slate-600">Query Queue content will be implemented in WP-018.</p>
    </>
  );
}
