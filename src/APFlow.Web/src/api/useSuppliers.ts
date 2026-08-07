import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { supplierClient } from '@/api/supplierClient';
import type { SaveSupplierRequest, Supplier } from '@/types/supplier';
import { ApiError } from '@/api/httpClient';

export interface SuppliersState {
  suppliers: Supplier[];
  isLoading: boolean;
  error: string | null;
  /** True while a create/update is in flight — disables the form's submit control. */
  isSubmitting: boolean;
  /** Message from the most recent failed create/update, if any — includes the real backend's 403 Supplier.CreditLimitForbidden message when a stale/non-FINANCE_MANAGER session hits it (WP-026 task 5). */
  submitError: string | null;
  /** True when the most recent submit failure was specifically the backend's 403 Supplier.CreditLimitForbidden — lets the form surface a targeted message rather than a generic one. */
  submitErrorIsCreditLimitForbidden: boolean;
  /** Creates a supplier and reloads the list on success. Returns the created supplier, or null on failure (see `submitError`). */
  createSupplier: (request: SaveSupplierRequest) => Promise<Supplier | null>;
  /** Updates a supplier and reloads the list on success. Returns the updated supplier, or null on failure (see `submitError`). */
  updateSupplier: (id: string, request: SaveSupplierRequest) => Promise<Supplier | null>;
  retry: () => void;
}

const CREDIT_LIMIT_FORBIDDEN_CODE = 'Supplier.CreditLimitForbidden';

/**
 * Loads every supplier visible to the acting tenant and exposes
 * create/update mutations for WP-027's Supplier Management UI
 * (`SuppliersPage`/`SupplierForm`). Reloads the full list from the client
 * after a successful mutation, same "trust what the server persisted, not
 * an optimistic guess" convention `useInvoiceNotes.addNote` already
 * established, rather than splicing the returned record into local state.
 *
 * `submitErrorIsCreditLimitForbidden` is surfaced separately from the
 * plain error message (task 7 of this WP's brief): the UI already hides
 * the editable Credit Limit input from a non-FINANCE_MANAGER caller, so
 * this real 403 should be rare — but a role held at page-load time can go
 * stale mid-session (e.g. a group re-assignment), and the backend's
 * rejection is the actual security boundary, not the frontend's field
 * split. This must not crash the page — it's caught and surfaced as a
 * normal form error like any other.
 */
export function useSuppliers(): SuppliersState {
  const { user } = useAuth();
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [submitErrorIsCreditLimitForbidden, setSubmitErrorIsCreditLimitForbidden] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);

  const loadSuppliers = useCallback(async (signal: { cancelled: boolean }) => {
    setIsLoading(true);
    setError(null);
    try {
      const result = await supplierClient.getAll();
      if (signal.cancelled) return;
      setSuppliers(result);
      setIsLoading(false);
    } catch {
      if (!signal.cancelled) {
        setError('Unable to load suppliers. Please try again.');
        setIsLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    if (!user) return;

    const signal = { cancelled: false };
    void loadSuppliers(signal);

    return () => {
      signal.cancelled = true;
    };
  }, [user, reloadToken, loadSuppliers]);

  const createSupplier = useCallback(
    async (request: SaveSupplierRequest): Promise<Supplier | null> => {
      setIsSubmitting(true);
      setSubmitError(null);
      setSubmitErrorIsCreditLimitForbidden(false);
      try {
        const result = await supplierClient.create(request);
        const signal = { cancelled: false };
        await loadSuppliers(signal);
        return result;
      } catch (err) {
        setSubmitErrorIsCreditLimitForbidden(err instanceof ApiError && err.code === CREDIT_LIMIT_FORBIDDEN_CODE);
        setSubmitError(err instanceof Error ? err.message : 'Unable to save this supplier. Please try again.');
        return null;
      } finally {
        setIsSubmitting(false);
      }
    },
    [loadSuppliers],
  );

  const updateSupplier = useCallback(
    async (id: string, request: SaveSupplierRequest): Promise<Supplier | null> => {
      setIsSubmitting(true);
      setSubmitError(null);
      setSubmitErrorIsCreditLimitForbidden(false);
      try {
        const result = await supplierClient.update(id, request);
        const signal = { cancelled: false };
        await loadSuppliers(signal);
        return result;
      } catch (err) {
        setSubmitErrorIsCreditLimitForbidden(err instanceof ApiError && err.code === CREDIT_LIMIT_FORBIDDEN_CODE);
        setSubmitError(err instanceof Error ? err.message : 'Unable to save this supplier. Please try again.');
        return null;
      } finally {
        setIsSubmitting(false);
      }
    },
    [loadSuppliers],
  );

  const retry = useCallback(() => setReloadToken((t) => t + 1), []);

  return {
    suppliers: user ? suppliers : [],
    isLoading: user ? isLoading : false,
    error: user ? error : null,
    isSubmitting,
    submitError,
    submitErrorIsCreditLimitForbidden,
    createSupplier,
    updateSupplier,
    retry,
  };
}
