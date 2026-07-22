interface PageHeadingProps {
  title: string;
  description?: string;
}

/** Consistent page-level heading used by each top-level route's placeholder content. */
export function PageHeading({ title, description }: PageHeadingProps) {
  return (
    <div className="mb-6">
      <h1 className="text-xl font-semibold text-ink-900">{title}</h1>
      {description && <p className="mt-1 text-sm text-slate-600">{description}</p>}
    </div>
  );
}
