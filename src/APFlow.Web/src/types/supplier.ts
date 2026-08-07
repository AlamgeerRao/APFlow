/**
 * Client-side shapes for WP-027 (Supplier Management UI), mapping onto
 * WP-026's real `SupplierDto`/`SaveSupplierRequest`
 * (`src/APFlow.Application/DTOs/SupplierDto.cs`) — field names confirmed
 * directly against that file, camelCased on the wire per this codebase's
 * usual ASP.NET Core System.Text.Json convention (same as every other real
 * client, e.g. `ingestionIssueClient.ts`).
 */

/** Mirrors `SupplierStatusCodes` (`src/APFlow.Domain/Common/Constants/SupplierStatusCodes.cs`) — do not invent additional values. */
export const SUPPLIER_STATUS_ACTIVE = 'ACTIVE';
export const SUPPLIER_STATUS_INACTIVE = 'INACTIVE';

export type SupplierStatus = typeof SUPPLIER_STATUS_ACTIVE | typeof SUPPLIER_STATUS_INACTIVE;

/**
 * Client-side mirrors of `FieldLimits`
 * (`src/APFlow.Application/Common/FieldLimits.cs`) — validated here too so a
 * rejection surfaces immediately in the form rather than only after a round
 * trip to the server. If the backend's limits change, these must be updated
 * to match (same "silently accept values the server will still reject"
 * risk that file's own doc comment calls out).
 */
export const SUPPLIER_NAME_MAX_LENGTH = 256;
export const SUPPLIER_CODE_MAX_LENGTH = 32;
export const SUPPLIER_EMAIL_MAX_LENGTH = 256;
export const SUPPLIER_PHONE_MAX_LENGTH = 32;
export const SUPPLIER_ACCOUNTING_REFERENCE_MAX_LENGTH = 64;

/** Read shape for a supplier — mirrors `SupplierDto`. */
export interface Supplier {
  id: string;
  name: string;
  code: string | null;
  email: string | null;
  phone: string | null;
  creditLimit: number | null;
  paymentTermsDays: number | null;
  accountingReference: string | null;
  status: string;
  createdAtUtc: string;
}

/**
 * Create/update request shape — mirrors `SaveSupplierRequest`. Every field
 * except `name`/`status` is nullable (not merely optional): a PUT is a
 * full-field-replace on the backend (`ISupplierService.UpdateAsync`'s own
 * contract), so clearing a field means sending `null`, not omitting the
 * key.
 */
export interface SaveSupplierRequest {
  name: string;
  code: string | null;
  email: string | null;
  phone: string | null;
  creditLimit: number | null;
  paymentTermsDays: number | null;
  accountingReference: string | null;
  status: string;
}
