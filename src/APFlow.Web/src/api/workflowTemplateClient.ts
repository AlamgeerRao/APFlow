import type { WorkflowTemplate } from '@/types/workflowTemplate';
import { platformDefaultWorkflowTemplate } from '@/api/fixtures/platformDefault.workflowTemplate';
import { gbSkipsWorkflowTemplate } from '@/api/fixtures/gbSkips.workflowTemplate';

/**
 * Client-side contract for fetching the acting tenant's Invoice
 * WorkflowTemplate. WP-050 (backend) has not shipped an endpoint yet — see
 * docs/WP-014-Dashboard-Shell-Decisions.md for the proposed HTTP contract
 * this interface is expected to map onto (GET /api/tenants/current/workflow-template).
 *
 * Consumers (e.g. LeftNav) depend on this interface, not on the fixture
 * implementation below, so swapping in the real APFlow.Api-backed client
 * once WP-050 ships requires no change to any consumer.
 */
export interface WorkflowTemplateClient {
  getCurrentWorkflowTemplate(tenantId: string): Promise<WorkflowTemplate>;
}

const fixturesByTenantId: Record<string, WorkflowTemplate> = {
  [platformDefaultWorkflowTemplate.tenantId]: platformDefaultWorkflowTemplate,
  [gbSkipsWorkflowTemplate.tenantId]: gbSkipsWorkflowTemplate,
};

/**
 * Temporary fixture-backed implementation, used until WP-050's
 * `GET /api/tenants/current/workflow-template` endpoint exists.
 *
 * Falls back to the platform-default template for any tenantId it does not
 * recognise, matching the backend rule that platform default is what every
 * tenant gets unless they have their own WorkflowTemplate.
 */
export class FixtureWorkflowTemplateClient implements WorkflowTemplateClient {
  async getCurrentWorkflowTemplate(tenantId: string): Promise<WorkflowTemplate> {
    return fixturesByTenantId[tenantId] ?? platformDefaultWorkflowTemplate;
  }
}

/**
 * The client instance the app uses. Swap this single line for a real
 * HTTP-backed implementation once WP-050 ships — no other file needs to
 * change.
 */
export const workflowTemplateClient: WorkflowTemplateClient = new FixtureWorkflowTemplateClient();
