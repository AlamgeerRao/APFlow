import { useId, useState } from 'react';
import type { FormEvent } from 'react';
import type { SaveSupplierRequest, Supplier } from '@/types/supplier';
import {
  SUPPLIER_ACCOUNTING_REFERENCE_MAX_LENGTH,
  SUPPLIER_CODE_MAX_LENGTH,
  SUPPLIER_EMAIL_MAX_LENGTH,
  SUPPLIER_NAME_MAX_LENGTH,
  SUPPLIER_PHONE_MAX_LENGTH,
  SUPPLIER_STATUS_ACTIVE,
  SUPPLIER_STATUS_INACTIVE,
} from '@/types/supplier';

interface SupplierFormProps {
  /** Omitted for "create a new supplier"; supplied for "edit this supplier". */
  supplier?: Supplier;
  /**
   * WP-026 task 5 / WP-027 task 3: only a caller holding FINANCE_MANAGER
   * may set or change Credit Limit. Determines which of the two Credit
   * Limit renderings below is shown — this is a UX convenience only, the
   * backend's own 403 (see `useSuppliers.ts`'s
   * `submitErrorIsCreditLimitForbidden`) is the real enforcement.
   */
  isFinanceManager: boolean;
  onSubmit: (request: SaveSupplierRequest) => Promise<Supplier | null>;
  onCancel: () => void;
  isSubmitting: boolean;
  submitError: string | null;
  submitErrorIsCreditLimitForbidden: boolean;
}

interface FormErrors {
  name?: string;
  code?: string;
  email?: string;
  phone?: string;
  creditLimit?: string;
  paymentTermsDays?: string;
  accountingReference?: string;
}

/** Renders a plain, non-empty numeric credit limit as a fixed 2dp string for the input's default value — an empty string for a null/never-set limit. */
function creditLimitInputValue(creditLimit: number | null | undefined): string {
  return creditLimit == null ? '' : String(creditLimit);
}

/**
 * Formats a credit limit for the read-only display shown to a
 * non-FINANCE_MANAGER caller. `Supplier` has no currency field of its own
 * (unlike `Invoice`) — plain grouped-number formatting, not
 * `formatCurrency`, which requires an ISO currency code this entity
 * doesn't carry.
 */
function formatCreditLimitReadOnly(creditLimit: number | null | undefined): string {
  if (creditLimit == null) return 'Not set';
  return new Intl.NumberFormat('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(creditLimit);
}

/**
 * Create/edit form for a supplier (WP-027 tasks 1–4), reused for both
 * "Add supplier" (no `supplier` prop) and "Edit" (per-group action in
 * `SupplierGroupList`). Every field except Credit Limit is a standard
 * editable input available to any user with base access (task 2).
 *
 * Credit Limit (task 3) — visible to everyone, per the explicit brief
 * ("do not hide it"): a FINANCE_MANAGER caller gets a normal editable
 * number input; anyone else gets plain read-only text, deliberately never
 * a `disabled` input — the brief calls that out by name as the wrong
 * pattern ("do not show a disabled/greyed-out input... it must look
 * intentionally read-only, not broken"). A non-FINANCE_MANAGER submission
 * always round-trips the supplier's existing Credit Limit unchanged
 * (`null` on create, since there is nothing to round-trip yet), matching
 * `SupplierService`'s own "same value is not a change" semantics — see
 * `SupplierService.UpdateAsync`'s doc comment.
 */
export function SupplierForm({
  supplier,
  isFinanceManager,
  onSubmit,
  onCancel,
  isSubmitting,
  submitError,
  submitErrorIsCreditLimitForbidden,
}: SupplierFormProps) {
  const isEditMode = supplier !== undefined;
  const formId = useId();

  const [name, setName] = useState(supplier?.name ?? '');
  const [code, setCode] = useState(supplier?.code ?? '');
  const [email, setEmail] = useState(supplier?.email ?? '');
  const [phone, setPhone] = useState(supplier?.phone ?? '');
  const [creditLimitInput, setCreditLimitInput] = useState(creditLimitInputValue(supplier?.creditLimit));
  const [paymentTermsDays, setPaymentTermsDays] = useState(
    supplier?.paymentTermsDays != null ? String(supplier.paymentTermsDays) : '',
  );
  const [accountingReference, setAccountingReference] = useState(supplier?.accountingReference ?? '');
  const [status, setStatus] = useState(supplier?.status ?? SUPPLIER_STATUS_ACTIVE);
  const [validationErrors, setValidationErrors] = useState<FormErrors>({});

  function toNullableString(value: string): string | null {
    const trimmed = value.trim();
    return trimmed.length === 0 ? null : trimmed;
  }

  function validate(): FormErrors {
    const errors: FormErrors = {};

    if (name.trim().length === 0) {
      errors.name = 'Enter a supplier name.';
    } else if (name.length > SUPPLIER_NAME_MAX_LENGTH) {
      errors.name = `Supplier name must not exceed ${SUPPLIER_NAME_MAX_LENGTH} characters.`;
    }

    if (code.length > SUPPLIER_CODE_MAX_LENGTH) {
      errors.code = `Supplier code must not exceed ${SUPPLIER_CODE_MAX_LENGTH} characters.`;
    }

    if (email.length > SUPPLIER_EMAIL_MAX_LENGTH) {
      errors.email = `Email must not exceed ${SUPPLIER_EMAIL_MAX_LENGTH} characters.`;
    }

    if (phone.length > SUPPLIER_PHONE_MAX_LENGTH) {
      errors.phone = `Phone must not exceed ${SUPPLIER_PHONE_MAX_LENGTH} characters.`;
    }

    if (accountingReference.length > SUPPLIER_ACCOUNTING_REFERENCE_MAX_LENGTH) {
      errors.accountingReference = `Accounting reference must not exceed ${SUPPLIER_ACCOUNTING_REFERENCE_MAX_LENGTH} characters.`;
    }

    if (isFinanceManager && creditLimitInput.trim().length > 0) {
      const parsed = Number(creditLimitInput);
      if (Number.isNaN(parsed) || parsed < 0) {
        errors.creditLimit = 'Credit limit must be a non-negative number.';
      }
    }

    if (paymentTermsDays.trim().length > 0) {
      const parsed = Number(paymentTermsDays);
      if (!Number.isInteger(parsed) || parsed < 0) {
        errors.paymentTermsDays = 'Payment terms (days) must be a non-negative whole number.';
      }
    }

    return errors;
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const errors = validate();
    setValidationErrors(errors);
    if (Object.keys(errors).length > 0) return;

    // Non-FINANCE_MANAGER callers never see an editable Credit Limit input
    // (task 3) — their submission always round-trips the existing value
    // unchanged (null on create, since nothing exists yet to round-trip),
    // so it can never itself trigger the backend's 403.
    const creditLimit = isFinanceManager
      ? creditLimitInput.trim().length === 0
        ? null
        : Number(creditLimitInput)
      : (supplier?.creditLimit ?? null);

    const request: SaveSupplierRequest = {
      name: name.trim(),
      code: toNullableString(code),
      email: toNullableString(email),
      phone: toNullableString(phone),
      creditLimit,
      paymentTermsDays: paymentTermsDays.trim().length === 0 ? null : Number(paymentTermsDays),
      accountingReference: toNullableString(accountingReference),
      status,
    };

    await onSubmit(request);
    // On failure, submitError (passed back in as a prop) is shown below and
    // the typed values are left in place so the user doesn't lose their
    // edits and can retry — same convention as AddNoteForm.
  }

  const inputClassName =
    'block w-full rounded-md border border-slate-200 p-2 text-sm text-ink-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400';
  const labelClassName = 'mb-1 block text-sm font-medium text-ink-900';

  return (
    <form
      onSubmit={handleSubmit}
      // Native HTML5 constraint validation (from the number inputs' `min`
      // attributes below) would otherwise silently swallow the submit
      // event before this component's own `validate()` ever runs, when a
      // value violates it (e.g. a negative payment-terms figure) —
      // validation here is deliberately all client-JS-driven (matching
      // AddNoteForm/ConfirmActionDialog's own validate-then-setState
      // pattern), not native browser validation UI.
      noValidate
      aria-label={isEditMode ? `Edit ${supplier.name}` : 'Add supplier'}
      className="rounded-md border border-slate-200 bg-white p-4"
    >
      <h3 className="mb-3 text-sm font-semibold text-ink-900">{isEditMode ? `Edit ${supplier.name}` : 'Add supplier'}</h3>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor={`${formId}-name`} className={labelClassName}>
            Name
          </label>
          <input
            id={`${formId}-name`}
            type="text"
            value={name}
            onChange={(event) => setName(event.target.value)}
            disabled={isSubmitting}
            aria-invalid={validationErrors.name ? true : undefined}
            className={inputClassName}
          />
          {validationErrors.name && (
            <p role="alert" className="mt-1 text-xs text-red-600">
              {validationErrors.name}
            </p>
          )}
        </div>

        <div>
          <label htmlFor={`${formId}-code`} className={labelClassName}>
            Code
          </label>
          <input
            id={`${formId}-code`}
            type="text"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            disabled={isSubmitting}
            className={inputClassName}
          />
          {validationErrors.code && (
            <p role="alert" className="mt-1 text-xs text-red-600">
              {validationErrors.code}
            </p>
          )}
        </div>

        <div>
          <label htmlFor={`${formId}-email`} className={labelClassName}>
            Email
          </label>
          <input
            id={`${formId}-email`}
            type="text"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            disabled={isSubmitting}
            className={inputClassName}
          />
          {validationErrors.email && (
            <p role="alert" className="mt-1 text-xs text-red-600">
              {validationErrors.email}
            </p>
          )}
        </div>

        <div>
          <label htmlFor={`${formId}-phone`} className={labelClassName}>
            Phone
          </label>
          <input
            id={`${formId}-phone`}
            type="text"
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            disabled={isSubmitting}
            className={inputClassName}
          />
          {validationErrors.phone && (
            <p role="alert" className="mt-1 text-xs text-red-600">
              {validationErrors.phone}
            </p>
          )}
        </div>

        <div>
          <label htmlFor={`${formId}-payment-terms`} className={labelClassName}>
            Payment terms (days)
          </label>
          <input
            id={`${formId}-payment-terms`}
            type="number"
            min={0}
            step={1}
            value={paymentTermsDays}
            onChange={(event) => setPaymentTermsDays(event.target.value)}
            disabled={isSubmitting}
            className={inputClassName}
          />
          {validationErrors.paymentTermsDays && (
            <p role="alert" className="mt-1 text-xs text-red-600">
              {validationErrors.paymentTermsDays}
            </p>
          )}
        </div>

        <div>
          <label htmlFor={`${formId}-accounting-reference`} className={labelClassName}>
            Accounting reference
          </label>
          <input
            id={`${formId}-accounting-reference`}
            type="text"
            value={accountingReference}
            onChange={(event) => setAccountingReference(event.target.value)}
            disabled={isSubmitting}
            className={inputClassName}
          />
          {validationErrors.accountingReference && (
            <p role="alert" className="mt-1 text-xs text-red-600">
              {validationErrors.accountingReference}
            </p>
          )}
        </div>

        <div>
          <label htmlFor={`${formId}-status`} className={labelClassName}>
            Status
          </label>
          <select
            id={`${formId}-status`}
            value={status}
            onChange={(event) => setStatus(event.target.value)}
            disabled={isSubmitting}
            className={inputClassName}
          >
            <option value={SUPPLIER_STATUS_ACTIVE}>Active</option>
            <option value={SUPPLIER_STATUS_INACTIVE}>Inactive</option>
          </select>
        </div>

        {isFinanceManager ? (
          <div>
            <label htmlFor={`${formId}-credit-limit`} className={labelClassName}>
              Credit limit
            </label>
            <input
              id={`${formId}-credit-limit`}
              type="number"
              min={0}
              step={0.01}
              value={creditLimitInput}
              onChange={(event) => setCreditLimitInput(event.target.value)}
              disabled={isSubmitting}
              aria-invalid={validationErrors.creditLimit ? true : undefined}
              className={inputClassName}
            />
            {validationErrors.creditLimit && (
              <p role="alert" className="mt-1 text-xs text-red-600">
                {validationErrors.creditLimit}
              </p>
            )}
          </div>
        ) : (
          <div>
            <span className={labelClassName}>Credit limit</span>
            {/*
              Deliberately plain read-only text, not a disabled input — the
              WP brief explicitly rules out a greyed-out/disabled control
              here ("it must look intentionally read-only, not broken").
              `role="note"`-free `<p>` reads naturally to assistive tech as
              static content, not a form control a screen reader user might
              expect to be able to interact with.
            */}
            <p data-testid="credit-limit-readonly" className="rounded-md border border-transparent bg-slate-50 p-2 text-sm text-ink-900">
              {formatCreditLimitReadOnly(supplier?.creditLimit)}
            </p>
            <p className="mt-1 text-xs text-slate-400">Only a Full Approver (Finance Manager) can change the credit limit.</p>
          </div>
        )}
      </div>

      {submitError && (
        <p role="alert" className="mt-3 text-sm text-red-600">
          {submitErrorIsCreditLimitForbidden
            ? `You don't have permission to change the credit limit. ${submitError}`
            : submitError}
        </p>
      )}

      <div className="mt-4 flex gap-2">
        <button
          type="submit"
          disabled={isSubmitting}
          className="rounded-md bg-accent-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {isSubmitting ? 'Saving...' : isEditMode ? 'Save changes' : 'Add supplier'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          disabled={isSubmitting}
          className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-ink-900 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:text-slate-400"
        >
          Cancel
        </button>
      </div>
    </form>
  );
}
