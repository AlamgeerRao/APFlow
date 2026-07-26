interface SupplierFolderFiltersProps {
  search: string;
  onSearchChange: (value: string) => void;
  supplier: string | undefined;
  onSupplierChange: (value: string | undefined) => void;
  supplierOptions: string[];
}

/** Search box and supplier filter dropdown for the Supplier & Folder Views page (WP-019 tasks 4). */
export function SupplierFolderFilters({
  search,
  onSearchChange,
  supplier,
  onSupplierChange,
  supplierOptions,
}: SupplierFolderFiltersProps) {
  return (
    <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="flex-1">
        <label htmlFor="supplier-folder-search" className="sr-only">
          Search by supplier or invoice number
        </label>
        <input
          id="supplier-folder-search"
          type="search"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Search by supplier or invoice number…"
          className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        />
      </div>

      <div>
        <label htmlFor="supplier-filter" className="sr-only">
          Filter by supplier
        </label>
        <select
          id="supplier-filter"
          value={supplier ?? ''}
          onChange={(event) => onSupplierChange(event.target.value === '' ? undefined : event.target.value)}
          className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 sm:w-56"
        >
          <option value="">All suppliers</option>
          {supplierOptions.map((name) => (
            <option key={name} value={name}>
              {name}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}
