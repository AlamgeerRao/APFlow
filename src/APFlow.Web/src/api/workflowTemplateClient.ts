import type { WorkflowTemplate } from '@/types/workflowTemplate';
import { platformDefaultWorkflowTemplate } from '@/api/fixtures/platformDefault.workflowTemplate';
import { gbSkipsWorkflowTemplate } from '@/api/fixtures/gbSkips.workflowTemplate';
import { httpClient } from '@/api/httpClient';

/**
 * Client-side contract for fetching the acting tenant's Invoice
 * WorkflowTemplate.
 *
 * Consumers (e.g. LeftNav) depend on this interface, not on the fixture
 * implementation below, so swapping in the real APFlow.Api-backed client
 * requires no change to any consumer.
 */
export interface WorkflowTemplateClient {
  getCurrentWorkflowTemplate(tenantId: string): Promise<WorkflowTemplate>;
}

const fixturesByTenantId: Record<string, WorkflowTemplate> = {
  [platformDefaultWorkflowTemplate.tenantId]: platformDefaultWorkflowTemplate,
  [gbSkipsWorkflowTemplate.tenantId]: gbSkipsWorkflowTemplate,
};

/**
 * Temporary fixture-backed implementation. Falls back to the platform-default
 * template for any tenantId it does not recognise, matching the backend rule
 * that platform default is what every tenant gets unless they have their own
 * WorkflowTemplate. Kept only for its existing unit test coverage - see
 * `HttpWorkflowTemplateClient` below for what the app actually uses.
 */
export class FixtureWorkflowTemplateClient implements WorkflowTemplateClient {
  async getCurrentWorkflowTemplate(tenantId: string): Promise<WorkflowTemplate> {
    return fixturesByTenantId[tenantId] ?? platformDefaultWorkflowTemplate;
  }
}

/**
 * Real DTO for `GET /api/workflow-template` (WP-075). Field names confirmed
 * directly against `WorkflowTemplateDto`/`WorkflowStatusDto` (`WorkflowTemplateDto.cs`):
 * `sortOrder` (not `order`), `name` (the template's own name, not to be
 * confused with each status's own `name`), `isTenantSpecific`/`id` (no
 * `tenantId` field on the wire at all - see `mapWorkflowTemplate` below for
 * why that's fine).
 */
interface WorkflowStatusResponseDto {
  code: string;
  name: string;
  isTerminal: boolean;
  sortOrder: number;
}

interface WorkflowTemplateResponseDto {
  id: string;
  domainName: string;
  name: string;
  isTenantSpecific: boolean;
  statuses: WorkflowStatusResponseDto[];
  transitions: { fromStatusCode: string; toStatusCode: string }[];
}

/**
 * Maps the real response to our `WorkflowTemplate` shape. `tenantId` has no
 * wire equivalent - the real endpoint resolves the acting tenant from the
 * caller's own Bearer token server-side (same reasoning as every other real
 * client this session - `HttpInvoiceClient`, `HttpSupplierFolderClient`),
 * never from a client-supplied value, so there is nothing to read it back
 * from. The `tenantId` this function was called with is echoed back instead:
 * it's already known to be correct (the caller IS that tenant), and no real
 * consumer currently reads `WorkflowTemplate.tenantId` at all (confirmed by
 * search) - this is filled in for shape-completeness, not because anything
 * depends on its exact value.
 */
function mapWorkflowTemplate(tenantId: string, dto: WorkflowTemplateResponseDto): WorkflowTemplate {
  return {
    tenantId,
    templateName: dto.name,
    statuses: dto.statuses.map((status) => ({
      code: status.code,
      name: status.name,
      isTerminal: status.isTerminal,
      order: status.sortOrder,
    })),
  };
}

/**
 * Real HTTP-backed implementation (WP-075), calling `GET /api/workflow-template`.
 * `tenantId` is accepted (matching the shared `WorkflowTemplateClient`
 * interface every consumer already depends on) but deliberately NOT sent as a
 * query parameter - same reasoning as every other real client this session:
 * the API resolves the acting tenant from the caller's own Bearer token,
 * never from client-supplied input.
 */
export class HttpWorkflowTemplateClient implements WorkflowTemplateClient {
  async getCurrentWorkflowTemplate(tenantId: string): Promise<WorkflowTemplate> {
    const response = await httpClient.get<WorkflowTemplateResponseDto>('/api/workflow-template');
    return mapWorkflowTemplate(tenantId, response);
  }
}

/**
 * The client instance the app uses. `HttpWorkflowTemplateClient` as of
 * WP-075 - swap this single line back to `new FixtureWorkflowTemplateClient()`
 * if the real API becomes unreachable during local development.
 */
export const workflowTemplateClient: WorkflowTemplateClient = new HttpWorkflowTemplateClient();
