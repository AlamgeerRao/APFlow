import type { FolderSummary } from '@/types/supplierFolder';

interface FolderListProps {
  folders: FolderSummary[];
  selectedFolder: string | undefined;
  onSelectFolder: (statusCode: string | undefined) => void;
  totalCount: number;
}

function chipClasses(isActive: boolean): string {
  return [
    'inline-flex items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600',
    isActive
      ? 'border-ink-900 bg-ink-900 text-white'
      : 'border-slate-200 bg-white text-ink-900 hover:bg-slate-100',
  ].join(' ');
}

/**
 * Folder list built from the acting tenant's WorkflowTemplate (WP-050),
 * not a hardcoded array (WP-019 task 1) — GB Skips' extra folders
 * (Checked & Ready to Approve, Needs Review by Febina) appear here
 * automatically because `folders` comes from `useSupplierFolderView`,
 * which sources it from `supplierFolderClient.getFolderCounts`, which
 * reads the tenant's own WorkflowTemplate.
 */
export function FolderList({ folders, selectedFolder, onSelectFolder, totalCount }: FolderListProps) {
  return (
    <nav aria-label="Folders" className="flex flex-wrap gap-2">
      <button type="button" onClick={() => onSelectFolder(undefined)} className={chipClasses(!selectedFolder)}>
        All Folders
        <span className="rounded-full bg-black/10 px-1.5 py-0.5 text-xs">{totalCount}</span>
      </button>
      {folders.map((folder) => (
        <button
          key={folder.statusCode}
          type="button"
          onClick={() => onSelectFolder(folder.statusCode)}
          className={chipClasses(selectedFolder === folder.statusCode)}
        >
          {folder.statusLabel}
          <span className="rounded-full bg-black/10 px-1.5 py-0.5 text-xs">{folder.count}</span>
        </button>
      ))}
    </nav>
  );
}
