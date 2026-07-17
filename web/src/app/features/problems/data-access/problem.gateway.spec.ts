import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AlgoJudgeApi } from '../../../core/api/generated/algo-judge-api';
import { ProblemGateway } from './problem.gateway';
import { DEFAULT_PROBLEM_QUERY } from './problem.models';

describe('ProblemGateway', () => {
  const invoke = vi.fn();
  let gateway: ProblemGateway;

  beforeEach(() => {
    invoke.mockReset();
    invoke.mockReturnValue(
      of({ items: [], pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0 }),
    );
    TestBed.configureTestingModule({
      providers: [ProblemGateway, { provide: AlgoJudgeApi, useValue: { invoke } }],
    });
    gateway = TestBed.inject(ProblemGateway);
  });

  it('maps domain filters to the generated API boundary', () => {
    gateway
      .list({
        ...DEFAULT_PROBLEM_QUERY,
        search: 'graph',
        difficulty: 'Medium',
        tags: ['graph'],
        solved: true,
        pageNumber: 2,
      })
      .subscribe();

    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      Search: 'graph',
      Difficulty: 2,
      Tags: ['graph'],
      Solved: true,
      PageNumber: 2,
      PageSize: 20,
    });
  });
});
