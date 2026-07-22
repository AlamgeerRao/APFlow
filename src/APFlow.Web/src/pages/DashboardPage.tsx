import { PageHeading } from '@/components/layout/PageHeading';

/** Placeholder route content. Dashboard business functionality is out of scope for WP-014. */
export function DashboardPage() {
  return (
    <>
      <PageHeading title="Dashboard" description="Overview of invoice processing activity." />
      <p className="text-sm text-slate-600">Dashboard content will be implemented in a later work package.</p>
    </>
  );
}
