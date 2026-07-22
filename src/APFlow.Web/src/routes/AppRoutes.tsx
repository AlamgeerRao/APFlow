import { Navigate, Route, Routes } from 'react-router-dom';
import { ProtectedRoute } from '@/auth/ProtectedRoute';
import { AppShell } from '@/components/layout/AppShell';
import { LoginPage } from '@/pages/LoginPage';
import { DashboardPage } from '@/pages/DashboardPage';
import { InboxPage } from '@/pages/InboxPage';
import { InvoiceQueuePage } from '@/pages/InvoiceQueuePage';
import { QueryQueuePage } from '@/pages/QueryQueuePage';
import { ApprovedPage } from '@/pages/ApprovedPage';
import { SuppliersPage } from '@/pages/SuppliersPage';
import { AdministrationPage } from '@/pages/AdministrationPage';
import { NotFoundPage } from '@/pages/NotFoundPage';

/**
 * Top-level route table. All routes under AppShell are protected; only
 * /login is public. Matches the static nav sections defined in navConfig,
 * plus /invoices/:statusCode for the data-driven Invoice Queue sub-links.
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />

      <Route
        element={
          <ProtectedRoute>
            <AppShell />
          </ProtectedRoute>
        }
      >
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/inbox" element={<InboxPage />} />
        <Route path="/invoices" element={<InvoiceQueuePage />} />
        <Route path="/invoices/:statusCode" element={<InvoiceQueuePage />} />
        <Route path="/queries" element={<QueryQueuePage />} />
        <Route path="/approved" element={<ApprovedPage />} />
        <Route path="/suppliers" element={<SuppliersPage />} />
        <Route path="/administration" element={<AdministrationPage />} />
      </Route>

      <Route path="/" element={<Navigate to="/dashboard" replace />} />
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  );
}
