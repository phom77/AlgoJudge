import { mapRun } from './run.mapper';

describe('run mapper', () => {
  it('maps numeric statuses and preserves public output and metrics', () => {
    const run = mapRun({
      id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
      problemId: '7',
      status: 1,
      stdout: '42\n',
      stderr: '',
      executionTimeMs: '12',
      memoryUsedKb: '2048',
      createdAt: '2026-07-18T00:00:00Z',
      finishedAt: '2026-07-18T00:00:01Z',
    });

    expect(run).toMatchObject({
      problemId: 7,
      status: 'Completed',
      stdout: '42\n',
      stderr: '',
      executionTimeMs: 12,
      memoryUsedKb: 2048,
    });
  });

  it('accepts string statuses without leaking unknown response properties', () => {
    const run = mapRun({
      id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
      problemId: 7,
      status: 'RuntimeError' as unknown as number,
      createdAt: '2026-07-18T00:00:00Z',
      privateInput: 'hidden',
    } as never);

    expect(run.status).toBe('RuntimeError');
    expect('privateInput' in run).toBe(false);
  });
});
