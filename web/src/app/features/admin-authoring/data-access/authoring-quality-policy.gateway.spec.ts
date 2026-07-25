import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeAdminApi } from '../../../core/api/admin-generated/algo-judge-admin-api';
import { AuthoringGateway } from './authoring.gateway';

describe('AuthoringGateway quality policy', () => {
  it('sends only configured group minima after bootstrapping CSRF', () => {
    const ensureToken = vi.fn().mockReturnValue(of(undefined));
    const invoke = vi.fn().mockReturnValue(of({}));
    TestBed.configureTestingModule({
      providers: [
        AuthoringGateway,
        { provide: AntiforgeryService, useValue: { ensureToken, invalidate: vi.fn() } },
        { provide: AlgoJudgeAdminApi, useValue: { invoke } },
      ],
    });

    TestBed.inject(AuthoringGateway)
      .updateQualityPolicy('11111111-1111-1111-1111-111111111111', {
        minimumTestCaseCount: 500,
        minimumHandwrittenCases: 1,
        minimumEdgeCases: 20,
        minimumRandomCases: 400,
        minimumAdversarialCases: 0,
        minimumStressCases: 0,
        requireEachDeclaredWrongSolutionKilled: true,
      })
      .subscribe();

    expect(ensureToken).toHaveBeenCalledOnce();
    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      revisionId: '11111111-1111-1111-1111-111111111111',
      body: {
        qualityPolicy: {
          minimumTestCaseCount: 500,
          minimumCasesByGroup: [
            { group: 'handwritten', minimumCaseCount: 1 },
            { group: 'edge', minimumCaseCount: 20 },
            { group: 'random', minimumCaseCount: 400 },
          ],
          requireEachDeclaredWrongSolutionKilled: true,
        },
      },
    });
  });
});
