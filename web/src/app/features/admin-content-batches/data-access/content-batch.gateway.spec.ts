import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeAdminApi } from '../../../core/api/admin-generated/algo-judge-admin-api';
import { ContentBatchGateway } from './content-batch.gateway';

describe('ContentBatchGateway', () => {
  const ensureToken = vi.fn();
  const invalidate = vi.fn();
  const invoke = vi.fn();
  let gateway: ContentBatchGateway;

  beforeEach(() => {
    ensureToken.mockReset();
    invalidate.mockReset();
    invoke.mockReset();
    ensureToken.mockReturnValue(of(undefined));
    invoke.mockReturnValue(of(batch()));
    TestBed.configureTestingModule({
      providers: [
        ContentBatchGateway,
        { provide: AntiforgeryService, useValue: { ensureToken, invalidate } },
        { provide: AlgoJudgeAdminApi, useValue: { invoke } },
      ],
    });
    gateway = TestBed.inject(ContentBatchGateway);
  });

  it('keeps list and detail reads free of CSRF bootstrapping', () => {
    gateway.list().subscribe();
    gateway.get('batch-1').subscribe();

    expect(ensureToken).not.toHaveBeenCalled();
    expect(invoke).toHaveBeenCalledTimes(2);
  });

  it('requires CSRF and sends only explicitly selected IDs for mutations', () => {
    gateway.retry('batch-1', ['item-1', 'item-2']).subscribe();
    gateway.publish('batch-1', ['revision-2']).subscribe();

    expect(ensureToken).toHaveBeenCalledTimes(2);
    expect(invoke.mock.calls[0]?.[1]).toEqual({
      batchId: 'batch-1',
      body: { itemIds: ['item-1', 'item-2'] },
    });
    expect(invoke.mock.calls[1]?.[1]).toEqual({
      batchId: 'batch-1',
      body: { revisionIds: ['revision-2'] },
    });
  });

  it('invalidates a cached CSRF token after an unsafe rejection', () => {
    invoke.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 403,
            error: { status: 403, code: 'csrf', title: 'Rejected' },
          }),
      ),
    );

    gateway.resume('batch-1').subscribe({ error: () => undefined });

    expect(invalidate).toHaveBeenCalledOnce();
  });
});

function batch() {
  return {
    id: 'batch-1',
    status: 3,
    counts: { total: 2, ready: 1, failed: 1 },
    items: [],
  };
}
