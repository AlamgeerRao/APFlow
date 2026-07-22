import { PageHeading } from '@/components/layout/PageHeading';

/** Placeholder route content. Administration business functionality is out of scope for WP-014. */
export function AdministrationPage() {
  return (
    <>
      <PageHeading title="Administration" description="Tenant, user, and workflow configuration." />
      <p className="text-sm text-slate-600">Administration content will be implemented in a later work package.</p>
    </>
  );
}
