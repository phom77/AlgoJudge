import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { ContentBatchGateway } from './content-batch.gateway';
import { ContentBatchPollingService } from './content-batch-polling.service';
import { ContentBatchStore } from './content-batch.store';

describe('ContentBatchStore', () => {
  const gateway = {
    list: vi.fn(),
    get: vi.fn(),
    start: vi.fn(),
    resume: vi.fn(),
    retry: vi.fn(),
    publish: vi.fn(),
  };
  const polling = { watch: vi.fn() };
  let store: ContentBatchStore;

  beforeEach(() => {
    for (const mock of Object.values(gateway)) mock.mockReset();
    polling.watch.mockReset();
    gateway.list.mockReturnValue(of({ items: [], totalCount: 0 }));
    gateway.get.mockReturnValue(of(detail()));
    gateway.retry.mockReturnValue(of(detail()));
    gateway.publish.mockReturnValue(of(detail()));
    polling.watch.mockReturnValue(of(detail()));
    TestBed.configureTestingModule({
      providers: [
        ContentBatchStore,
        { provide: ContentBatchGateway, useValue: gateway },
        { provide: ContentBatchPollingService, useValue: polling },
      ],
    });
    store = TestBed.inject(ContentBatchStore);
  });

  it('filters a 100-item batch locally by slug/title and status', () => {
    store.loadDetail('batch-1').subscribe();

    store.setQuery({ search: 'problem-089', status: 'ready' });

    expect(store.visibleItems()).toHaveLength(1);
    expect(store.visibleItems()[0]?.slug).toBe('problem-089');
  });

  it('publishes only explicitly selected Ready revisions', () => {
    store.loadDetail('batch-1').subscribe();
    store.toggleRevision('revision-001', true);
    store.toggleRevision('revision-099', true);

    store.publish('batch-1').subscribe();

    expect(gateway.publish).toHaveBeenCalledWith('batch-1', ['revision-001', 'revision-099']);
  });

  it('retries failed items without changing Ready selection', () => {
    store.loadDetail('batch-1').subscribe();
    store.toggleRevision('revision-001', true);

    store.retry('batch-1', ['item-091']).subscribe();

    expect(gateway.retry).toHaveBeenCalledWith('batch-1', ['item-091']);
    expect(store.selectedRevisionIds()).toEqual(['revision-001']);
  });
});

function detail() {
  return {
    id: 'batch-1',
    status: 3,
    counts: {
      total: 100,
      pending: 0,
      generating: 0,
      ready: 90,
      failed: 5,
      published: 0,
      skipped: 3,
    },
    items: Array.from({ length: 100 }, (_, index) => {
      const ordinal = index + 1;
      const suffix = ordinal.toString().padStart(3, '0');
      const failed = ordinal >= 91 && ordinal <= 95;
      const skipped = ordinal >= 96 && ordinal <= 98;
      const invalid = ordinal >= 99;
      return {
        id: `item-${suffix}`,
        revisionId: failed || skipped || invalid ? null : `revision-${suffix}`,
        ordinal,
        slug: `problem-${suffix}`,
        title: `Problem ${suffix}`,
        status: failed || invalid ? 4 : skipped ? 6 : 2,
        safeFailureCategory:
          ordinal === 99 ? 'duplicate_slug' : ordinal === 100 ? 'invalid_path' : null,
      };
    }),
  };
}
