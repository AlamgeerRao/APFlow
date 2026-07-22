import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import { Header } from '@/components/layout/Header';
import { LeftNav } from '@/components/layout/LeftNav';

/**
 * Main application shell: Header, Left navigation, Main content area.
 *
 * Responsive behaviour: the left nav is a permanent sidebar at md+ widths,
 * and an overlay panel toggled from the header on narrower viewports.
 */
export function AppShell() {
  const [isMobileNavOpen, setIsMobileNavOpen] = useState(false);

  return (
    <div className="flex h-screen flex-col bg-slate-50">
      <Header onToggleNav={() => setIsMobileNavOpen((previous) => !previous)} />

      <div className="flex min-h-0 flex-1">
        {/* Desktop sidebar */}
        <aside className="hidden w-64 flex-shrink-0 border-r border-slate-200 bg-white md:block">
          <LeftNav />
        </aside>

        {/* Mobile overlay nav */}
        {isMobileNavOpen && (
          <div className="fixed inset-0 z-40 md:hidden">
            <button
              type="button"
              aria-label="Close navigation menu"
              className="absolute inset-0 bg-ink-900/40"
              onClick={() => setIsMobileNavOpen(false)}
            />
            <aside className="relative z-50 h-full w-64 border-r border-slate-200 bg-white shadow-lg">
              <LeftNav />
            </aside>
          </div>
        )}

        <main className="min-w-0 flex-1 overflow-y-auto p-6" id="main-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
