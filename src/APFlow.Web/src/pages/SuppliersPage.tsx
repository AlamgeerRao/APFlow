import { PageHeading } from '@/components/layout/PageHeading';
import { useSupplierFolderView } from '@/api/useSupplierFolderView';
import { FolderList } from '@/components/supplierFolder/FolderList';
import { SupplierFolderFilters } from '@/components/supplierFolder/SupplierFolderFilters';
import { SupplierGroupList } from '@/components/supplierFolder/SupplierGroupList';
import { Pagination } from '@/components/invoiceQueue/Pagination';
import { SupplierFolderLoadingState, SupplierFolderErrorState } from '@/components/supplierFolder/SupplierFolderStates';

/**
 * Supplier & Folder Views (WP-019): browse invoices by supplier and by
 * workflow folder. The folder list is entirely data-driven from the
 * acting tenant's WorkflowTemplate (task 1) — see FolderList's own doc
 * comment. Selected folder/supplier/search/page are synced to the URL so
 * they're remembered across navigation (task 6) — see
 * useSupplierFolderView's doc comment.
 */
export function SuppliersPage() {
  const view = useSupplierFolderView();

  const totalCount = view.folderCounts.reduce((sum, folder) => sum + folder.count, 0);

  return (
    <>
      <PageHeading title="Suppliers" description="Browse invoices by supplier and workflow folder." />

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

      {view.isLoading && <SupplierFolderLoadingState />}

      {!view.isLoading && view.error && (
        <SupplierFolderErrorState message={view.error} onRetry={view.retry} />
      )}

      {!view.isLoading && !view.error && view.result && (
        <>
          <SupplierGroupList groups={view.result.groups} />
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
