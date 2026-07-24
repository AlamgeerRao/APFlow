import { describe, expect, it } from 'vitest';
import { FixtureWorkflowActionClient } from '@/api/workflowActionClient';

describe('FixtureWorkflowActionClient.getAvailableActions', () => {
  it('returns no actions for the platform-default tenant for any status (task 1: undocumented transition graph — see decisions doc)', async () => {
    const client = new FixtureWorkflowActionClient();

    const actions = await client.getAvailableActions('platform-default', 'AWAITING_REVIEW', ['AP_REVIEWER']);

    expect(actions).toEqual([]);
  });

  it('surfaces "Mark Checked & Ready to Approve" and "Escalate to Febina" for GB Skips from AWAITING_REVIEW (task 2)', async () => {
    const client = new FixtureWorkflowActionClient();

    const actions = await client.getAvailableActions('gb-skips', 'AWAITING_REVIEW', ['AP_REVIEWER']);

    expect(actions.map((a) => a.targetStatusLabel)).toEqual(
      expect.arrayContaining(['Mark Checked & Ready to Approve', 'Escalate to Febina']),
    );
  });

  it('includes Approve for a FINANCE_MANAGER user on a CHECKED_READY_TO_APPROVE invoice (task 3)', async () => {
    const client = new FixtureWorkflowActionClient();

    const actions = await client.getAvailableActions('gb-skips', 'CHECKED_READY_TO_APPROVE', ['FINANCE_MANAGER']);

    expect(actions.map((a) => a.targetStatusLabel)).toContain('Approve');
  });

  it('excludes Approve for an AP_REVIEWER user on a CHECKED_READY_TO_APPROVE invoice, without hiding the other available action (task 3)', async () => {
    const client = new FixtureWorkflowActionClient();

    const actions = await client.getAvailableActions('gb-skips', 'CHECKED_READY_TO_APPROVE', ['AP_REVIEWER']);

    expect(actions.map((a) => a.targetStatusLabel)).not.toContain('Approve');
    expect(actions.map((a) => a.targetStatusLabel)).toContain('Escalate to Febina');
  });

  it('returns the three resolution actions from NEEDS_REVIEW_FEBINA', async () => {
    const client = new FixtureWorkflowActionClient();

    const actions = await client.getAvailableActions('gb-skips', 'NEEDS_REVIEW_FEBINA', ['AP_REVIEWER']);

    expect(actions).toHaveLength(3);
    expect(actions.map((a) => a.targetStatusCode)).toEqual(
      expect.arrayContaining(['CHECKED_READY_TO_APPROVE', 'NEEDS_QUERY', 'REJECTED']),
    );
  });

  it('returns no actions for a status with no configured transitions', async () => {
    const client = new FixtureWorkflowActionClient();

    const actions = await client.getAvailableActions('gb-skips', 'PAID', ['FINANCE_MANAGER']);

    expect(actions).toEqual([]);
  });
});

describe('FixtureWorkflowActionClient.executeAction', () => {
  it('rejects execution for a user without the required role, with a message naming the role, and leaves the invoice unchanged (task 7)', async () => {
    const client = new FixtureWorkflowActionClient();

    await expect(
      client.executeAction('gb-skips', 'inv-gb-002', 'CHECKED_READY_TO_APPROVE', 'APPROVED', ['AP_REVIEWER']),
    ).rejects.toThrow(/Finance Manager \/ Decision-Maker/);

    // Still permitted for a Finance Manager afterwards — proves the
    // rejected attempt left the underlying status untouched (still
    // CHECKED_READY_TO_APPROVE, not already APPROVED).
    const actions = await client.getAvailableActions('gb-skips', 'CHECKED_READY_TO_APPROVE', ['FINANCE_MANAGER']);
    expect(actions.map((a) => a.targetStatusCode)).toContain('APPROVED');
  });

  it('rejects execution for a transition that is not configured from the given status', async () => {
    const client = new FixtureWorkflowActionClient();

    await expect(
      client.executeAction('gb-skips', 'inv-gb-004', 'AWAITING_REVIEW', 'APPROVED', ['FINANCE_MANAGER']),
    ).rejects.toThrow(/no longer available/i);
  });

  it('rejects execution for an unknown invoice id', async () => {
    const client = new FixtureWorkflowActionClient();

    await expect(
      client.executeAction('gb-skips', 'inv-does-not-exist', 'AWAITING_REVIEW', 'CHECKED_READY_TO_APPROVE', [
        'AP_REVIEWER',
      ]),
    ).rejects.toThrow(/not found/i);
  });

  // Runs last: this is the one test in this file that actually mutates
  // inv-gb-002's status, so every test above (which depends on it still
  // being CHECKED_READY_TO_APPROVE) must run before it.
  it('executes a permitted action and updates the invoice status', async () => {
    const client = new FixtureWorkflowActionClient();

    const result = await client.executeAction('gb-skips', 'inv-gb-002', 'CHECKED_READY_TO_APPROVE', 'APPROVED', [
      'FINANCE_MANAGER',
    ]);

    expect(result.newStatusCode).toBe('APPROVED');

    const actionsAfter = await client.getAvailableActions('gb-skips', 'APPROVED', ['FINANCE_MANAGER']);
    expect(actionsAfter).toEqual([]);
  });
});
