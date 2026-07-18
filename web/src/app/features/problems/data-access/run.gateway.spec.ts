import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeApi } from '../../../core/api/generated/algo-judge-api';
import { RunGateway } from './run.gateway';

describe('RunGateway', () => {
  const ensureToken = vi.fn();
  const invoke = vi.fn();
  let gateway: RunGateway;

  beforeEach(() => {
    ensureToken.mockReset();
    invoke.mockReset();
    ensureToken.mockReturnValue(of(undefined));
    invoke.mockReturnValue(of(response()));
    TestBed.configureTestingModule({
      providers: [
        RunGateway,
        { provide: AntiforgeryService, useValue: { ensureToken, invalidate: vi.fn() } },
        { provide: AlgoJudgeApi, useValue: { invoke } },
      ],
    });
    gateway = TestBed.inject(RunGateway);
  });

  it('bootstraps CSRF and creates exactly one cpp17 stdin run', () => {
    gateway.create('two-sum', 'int main() {}', { input: '1 2\n' }).subscribe();

    expect(ensureToken).toHaveBeenCalledOnce();
    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      slug: 'two-sum',
      body: { sourceCode: 'int main() {}', language: 'cpp17', input: '1 2\n' },
    });
  });

  it('sends named function arguments without converting their values', () => {
    gateway
      .create('two-sum', 'class Solution {};', { arguments: { nums: [1, 2], target: 3 } })
      .subscribe();

    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      slug: 'two-sum',
      body: {
        sourceCode: 'class Solution {};',
        language: 'cpp17',
        arguments: { nums: [1, 2], target: 3 },
      },
    });
  });
});

function response() {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    status: 0,
    createdAt: '2026-07-18T00:00:00Z',
  };
}
