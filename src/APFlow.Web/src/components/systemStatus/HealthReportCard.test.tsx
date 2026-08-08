import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HealthReportCard } from '@/components/systemStatus/HealthReportCard';

describe('HealthReportCard', () => {
  it('renders the title, description, and overall status', () => {
    render(<HealthReportCard title="Liveness" description="Is the process up." report={{ status: 'Healthy', checks: [] }} />);

    expect(screen.getByText('Liveness')).toBeInTheDocument();
    expect(screen.getByText('Is the process up.')).toBeInTheDocument();
    expect(screen.getByText('Healthy')).toBeInTheDocument();
  });

  it('renders one row per check, with its status and description', () => {
    render(
      <HealthReportCard
        title="Readiness"
        description="Can it serve requests."
        report={{
          status: 'Degraded',
          checks: [
            { name: 'database', status: 'Healthy', description: null },
            { name: 'graph-mailbox', status: 'Degraded', description: 'Mailbox connection could not be verified.' },
          ],
        }}
      />,
    );

    expect(screen.getByText('database')).toBeInTheDocument();
    expect(screen.getByText('graph-mailbox')).toBeInTheDocument();
    expect(screen.getByText('Mailbox connection could not be verified.')).toBeInTheDocument();
    expect(screen.getAllByText('Degraded').length).toBeGreaterThan(0);
  });

  it('renders no checks section when the report has none (liveness)', () => {
    render(<HealthReportCard title="Liveness" description="—" report={{ status: 'Healthy', checks: [] }} />);

    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('renders no status badge while the report is still loading (null)', () => {
    render(<HealthReportCard title="Readiness" description="—" report={null} />);

    expect(screen.queryByText('Healthy')).not.toBeInTheDocument();
  });
});
