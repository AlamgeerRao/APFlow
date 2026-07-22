import { PageHeading } from '@/components/layout/PageHeading';

/** Placeholder route content. Inbox business functionality is out of scope for WP-014. */
export function InboxPage() {
  return (
    <>
      <PageHeading title="Inbox" description="Incoming supplier emails and attachments." />
      <p className="text-sm text-slate-600">Inbox content will be implemented in a later work package.</p>
    </>
  );
}
