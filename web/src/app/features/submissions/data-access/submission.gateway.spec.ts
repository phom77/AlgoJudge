import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeApi } from '../../../core/api/generated/algo-judge-api';
import { SubmissionGateway } from './submission.gateway';

describe('SubmissionGateway', () => {
  const ensureToken = vi.fn();
  const invoke = vi.fn();
  let gateway: SubmissionGateway;

  beforeEach(() => {
    ensureToken.mockReset();
    invoke.mockReset();
    ensureToken.mockReturnValue(of(undefined));
    invoke.mockReturnValue(of(response()));
    TestBed.configureTestingModule({
      providers: [
        SubmissionGateway,
        { provide: AntiforgeryService, useValue: { ensureToken, invalidate: vi.fn() } },
        { provide: AlgoJudgeApi, useValue: { invoke } },
      ],
    });
    gateway = TestBed.inject(SubmissionGateway);
  });

  it('bootstraps CSRF and creates exactly one cpp17 submission request', () => {
    gateway.create(7, 'int main() {}').subscribe();

    expect(ensureToken).toHaveBeenCalledOnce();
    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      body: { problemId: 7, sourceCode: 'int main() {}', language: 'cpp17' },
    });
  });

  it('maps history verdict filters at the generated boundary', () => {
    invoke.mockReturnValue(
      of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }),
    );

    gateway
      .history({ problemId: 7, status: 'RuntimeError', pageNumber: 2, pageSize: 20 })
      .subscribe();

    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      ProblemId: 7,
      Status: 8,
      PageNumber: 2,
      PageSize: 20,
    });
  });
});

function response() {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    language: 'cpp17',
    status: 'Pending' as unknown as number,
    createdAt: '2026-07-17T00:00:00Z',
  };
}
