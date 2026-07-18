import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import { RunFlowStore } from './run-flow.store';
import { RunGateway } from './run.gateway';
import type { CodeRun } from './run.models';
import { RunPollingService } from './run-polling.service';

describe('RunFlowStore', () => {
  const isAuthenticated = signal(true);
  const create = vi.fn();
  const watch = vi.fn();
  let store: RunFlowStore;

  beforeEach(() => {
    create.mockReset();
    watch.mockReset();
    isAuthenticated.set(true);
    TestBed.configureTestingModule({
      providers: [
        RunFlowStore,
        { provide: AuthStore, useValue: { isAuthenticated } },
        { provide: RunGateway, useValue: { create } },
        { provide: RunPollingService, useValue: { watch } },
      ],
    });
    store = TestBed.inject(RunFlowStore);
  });

  it('prevents duplicate creation while a run is active', () => {
    const created = new Subject<CodeRun>();
    create.mockReturnValue(created);
    watch.mockImplementation((value: CodeRun) => of({ ...value, status: 'Completed' }));

    store.execute('two-sum', 'int main() {}', { input: '' }).subscribe();
    store.execute('two-sum', 'int main() {}', { input: '' }).subscribe();

    expect(create).toHaveBeenCalledOnce();
    expect(store.running()).toBe(true);
    created.next(run('Pending'));
    created.complete();
    expect(store.running()).toBe(false);
    expect(store.run()?.status).toBe('Completed');
  });

  it('rejects UTF-8 source larger than the backend limit without an API call', () => {
    store.execute('two-sum', '😀'.repeat(20_000), { input: '' }).subscribe();

    expect(create).not.toHaveBeenCalled();
    expect(store.problem()?.code).toBe('validation');
    expect(store.problem()?.detail).toContain('65,536 UTF-8 bytes');
  });
});

function run(status: CodeRun['status']): CodeRun {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    status,
    stdout: null,
    stderr: null,
    executionTimeMs: null,
    memoryUsedKb: null,
    createdAt: '2026-07-18T00:00:00Z',
    startedAt: null,
    finishedAt: null,
  };
}
