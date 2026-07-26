import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FolderList } from '@/components/supplierFolder/FolderList';

const folders = [
  { statusCode: 'AWAITING_REVIEW', statusLabel: 'Awaiting Review', count: 3 },
  { statusCode: 'NEEDS_QUERY', statusLabel: 'Needs Query', count: 1 },
];

describe('FolderList', () => {
  it('renders an "All Folders" chip plus one chip per folder, each with its count', () => {
    render(<FolderList folders={folders} selectedFolder={undefined} onSelectFolder={vi.fn()} totalCount={4} />);

    expect(screen.getByRole('button', { name: /All Folders/ })).toHaveTextContent('4');
    expect(screen.getByRole('button', { name: /Awaiting Review/ })).toHaveTextContent('3');
    expect(screen.getByRole('button', { name: /Needs Query/ })).toHaveTextContent('1');
  });

  it('calls onSelectFolder with the status code when a folder chip is clicked', async () => {
    const onSelectFolder = vi.fn();
    const user = userEvent.setup();
    render(<FolderList folders={folders} selectedFolder={undefined} onSelectFolder={onSelectFolder} totalCount={4} />);

    await user.click(screen.getByRole('button', { name: /Needs Query/ }));

    expect(onSelectFolder).toHaveBeenCalledWith('NEEDS_QUERY');
  });

  it('calls onSelectFolder with undefined when "All Folders" is clicked', async () => {
    const onSelectFolder = vi.fn();
    const user = userEvent.setup();
    render(<FolderList folders={folders} selectedFolder="NEEDS_QUERY" onSelectFolder={onSelectFolder} totalCount={4} />);

    await user.click(screen.getByRole('button', { name: /All Folders/ }));

    expect(onSelectFolder).toHaveBeenCalledWith(undefined);
  });

  it('renders GB Skips-style extra folders exactly when given, without any special-casing', () => {
    const gbFolders = [
      ...folders,
      { statusCode: 'CHECKED_READY_TO_APPROVE', statusLabel: 'Checked & Ready to Approve', count: 2 },
      { statusCode: 'NEEDS_REVIEW_FEBINA', statusLabel: 'Needs Review by Febina', count: 1 },
    ];
    render(<FolderList folders={gbFolders} selectedFolder={undefined} onSelectFolder={vi.fn()} totalCount={7} />);

    expect(screen.getByRole('button', { name: /Checked & Ready to Approve/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Needs Review by Febina/ })).toBeInTheDocument();
  });
});
