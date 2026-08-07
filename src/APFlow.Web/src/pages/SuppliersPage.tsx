import { useMemo, useState } from 'react';
import { PageHeading } from '@/components/layout/PageHeading';
import { useAuth } from '@/auth/useAuth';
import { useSupplierFolderView } from '@/api/useSupplierFolderView';
import { useSuppliers } from '@/api/useSuppliers';
import { FolderList } from '@/components/supplierFolder/FolderList';
import { SupplierFolderFilters } from '@/components/supplierFolder/SupplierFolderFilters';
import { SupplierGroupList } from '@/components/supplierFolder/SupplierGroupList';
import { SuppliersWithoutInvoices } from '@/components/supplierFolder/SuppliersWithoutInvoices';
import { SupplierForm } from '@/components/supplierFolder/SupplierForm';
import { Pagination } from '@/components/invoiceQueue/Pagination';
import { SupplierFolderLoadingState, SupplierFolderErrorState } from '@/components/supplierFolder/SupplierFolderStates';
import type { Supplier } from '@/types/supplier';

type FormState = { mode: 'closed' } | { mode: 'create' } | { mode: 'edit'; supplier: Supplier };

/**
 * Supplier & Folder Views (WP-019): browse invoices by supplier and by
 * workflow folder. The folder list is entirely data-driven from the
 * acting tenant's WorkflowTemplate (task 1) — see FolderList's own doc
 * comment. Selected folder/supplier/search/page are synced to the URL so
 * they're remembered across navigation (task 6) — see
 * useSupplierFolderView's doc comment.
 *
 * WP-027: extended (Option A, confirmed in Sprint2-Plan.md §3 — a second
 * screen was explicitly ruled out) with create/edit capability for
 * WP-026's supplier management fields. `useSuppliers` loads the real
 * `Supplier` records independently of `useSupplierFolderView`'s
 * invoice-grouped data (the two are different resources — a supplier can
 * exist with zero invoices, e.g. one just created here, and would never
 * appear in the invoice-driven group list below until its first invoice
 * arrives); `suppliersByName` bridges the two so each group heading can
 * resolve to its real `Supplier` record for editing (see
 * `SupplierGroupList`'s own doc comment).
 *
 * WP-093: the zero-invoice gap flagged above is closed by
 * `SuppliersWithoutInvoices` — a supplier from `useSuppliers` with no
 * matching entry in `view.supplierOptions` (the complete, unpaginated set
 * of supplier names with an invoice) is surfaced in its own small section,
 * so it's never invisible after creation.
 */
export function SuppliersPage() {
  const { user } = useAuth();
  const isFinanceManager = user?.roles.includes('FINANCE_MANAGER') ?? false;

  const view = useSupplierFolderView();
  const suppliers = useSuppliers();
  const [formState, setFormState] = useState<FormState>({ mode: 'closed' });

  const totalCount = view.folderCounts.reduce((sum, folder) => sum + folder.count, 0);

  // Case-insensitive/trimmed keying matches ResolveSupplierAsync's own
  // supplier-name resolution (WP-012) — see SupplierGroupList's doc comment.
  const suppliersByName = useMemo(() => {
    const map = new Map<string, Supplier>();
    for (const supplier of suppliers.suppliers) {
      map.set(supplier.name.trim().toLowerCase(), supplier);
    }
    return map;
  }, [suppliers.suppliers]);

  // WP-093: `view.supplierOptions` is the complete, unpaginated set of
  // supplier names with at least one invoice (unlike `view.result.groups`,
  // which is a 5-per-page slice) — the correct base to diff against so a
  // supplier isn't wrongly flagged as "no invoices yet" just for being on
  // a page the user hasn't scrolled to.
  const supplierNamesWithInvoices = useMemo(
    () => new Set(view.supplierOptions.map((name) => name.trim().toLowerCase())),
    [view.supplierOptions],
  );
  const suppliersWithNoInvoices = useMemo(
    () => suppliers.suppliers.filter((supplier) => !supplierNamesWithInvoices.has(supplier.name.trim().toLowerCase())),
    [suppliers.suppliers, supplierNamesWithInvoices],
  );

  async function handleSubmit(request: Parameters<typeof suppliers.createSupplier>[0]) {
    const result =
      formState.mode === 'edit'
        ? await suppliers.updateSupplier(formState.supplier.id, request)
        : await suppliers.createSupplier(request);

    if (result) {
      setFormState({ mode: 'closed' });
    }
    return result;
  }

  return (
    <>
      <div className="mb-6 flex items-start justify-between gap-4">
        <PageHeading title="Suppliers" description="Browse invoices by supplier and workflow folder." />
        {formState.mode === 'closed' && (
          <button
            type="button"
            onClick={() => setFormState({ mode: 'create' })}
            className="mt-1 shrink-0 rounded-md bg-accent-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            + Add supplier
          </button>
        )}
      </div>

      {formState.mode !== 'closed' && (
        <div className="mb-6">
          <SupplierForm
            key={formState.mode === 'edit' ? formState.supplier.id : 'create'}
            supplier={formState.mode === 'edit' ? formState.supplier : undefined}
            isFinanceManager={isFinanceManager}
            onSubmit={handleSubmit}
            onCancel={() => setFormState({ mode: 'closed' })}
            isSubmitting={suppliers.isSubmitting}
            submitError={suppliers.submitError}
            submitErrorIsCreditLimitForbidden={suppliers.submitErrorIsCreditLimitForbidden}
          />
        </div>
      )}

      <div className="mb-4">
        <FolderList
          folders={view.folderCounts}
          selectedFolder={view.folder}
          onSelectFolder={view.setFolder}
          totalCount={totalCount}
        />
      </div>

      <SupplierFolderFilters
        search={view.search}
        onSearchChange={view.setSearch}
        supplier={view.supplier}
        onSupplierChange={view.setSupplier}
        supplierOptions={view.supplierOptions}
      />

      <SuppliersWithoutInvoices
        suppliers={suppliersWithNoInvoices}
        onEdit={(supplier) => setFormState({ mode: 'edit', supplier })}
      />

      {view.isLoading && <SupplierFolderLoadingState />}

      {!view.isLoading && view.error && (
        <SupplierFolderErrorState message={view.error} onRetry={view.retry} />
      )}

      {!view.isLoading && !view.error && view.result && (
        <>
          <SupplierGroupList
            groups={view.result.groups}
            suppliersByName={suppliersByName}
            onEdit={(supplier) => setFormState({ mode: 'edit', supplier })}
          />
          <Pagination
            page={view.result.page}
            pageSize={view.result.pageSize}
            totalCount={view.result.totalSuppliers}
            onPageChange={view.setPage}
          />
        </>
      )}
    </>
  );
}
