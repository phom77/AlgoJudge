import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AntiforgeryService } from '../../../core/api/antiforgery.service';
import { AlgoJudgeAdminApi } from '../../../core/api/admin-generated/algo-judge-admin-api';
import { AuthoringGateway } from './authoring.gateway';

describe('AuthoringGateway', () => {
  const ensureToken = vi.fn();
  const invalidate = vi.fn();
  const invoke = vi.fn();
  let gateway: AuthoringGateway;

  beforeEach(() => {
    ensureToken.mockReset();
    invalidate.mockReset();
    invoke.mockReset();
    ensureToken.mockReturnValue(of(undefined));
    invoke.mockReturnValue(of(draftResponse()));
    TestBed.configureTestingModule({
      providers: [
        AuthoringGateway,
        { provide: AntiforgeryService, useValue: { ensureToken, invalidate } },
        { provide: AlgoJudgeAdminApi, useValue: { invoke } },
      ],
    });
    gateway = TestBed.inject(AuthoringGateway);
  });

  it('bootstraps CSRF before creating an authoring draft', () => {
    gateway.create(metadata()).subscribe();

    expect(ensureToken).toHaveBeenCalledOnce();
    expect(invoke).toHaveBeenCalledWith(expect.any(Function), {
      body: {
        slug: 'double-value',
        title: 'Double Value',
        statementMarkdown: 'Return twice the input.',
        constraintsMarkdown: '- The result fits in Int32.',
        difficulty: 'Easy',
        timeLimitMs: 1000,
        memoryLimitKb: 262144,
        samples: [{ input: '{"value":1}', expectedOutput: '2' }],
      },
    });
  });

  it('does not bootstrap CSRF for safe authoring reads', () => {
    gateway.get('11111111-1111-1111-1111-111111111111').subscribe();

    expect(ensureToken).not.toHaveBeenCalled();
    expect(invoke).toHaveBeenCalledOnce();
  });

  it('invalidates the cached token after an unsafe CSRF rejection', () => {
    invoke.mockReturnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 403,
            error: {
              status: 403,
              code: 'csrf',
              title: 'Antiforgery validation failed.',
              type: 'urn:algojudge:error:csrf',
            },
          }),
      ),
    );

    gateway.publish('11111111-1111-1111-1111-111111111111').subscribe({
      error: () => undefined,
    });

    expect(invalidate).toHaveBeenCalledOnce();
  });
});

function metadata() {
  return {
    slug: 'double-value',
    title: 'Double Value',
    statementMarkdown: 'Return twice the input.',
    constraintsMarkdown: '- The result fits in Int32.',
    difficulty: 'Easy' as const,
    timeLimitMs: 1000,
    memoryLimitKb: 262144,
    sampleInput: '{"value":1}',
    sampleOutput: '2',
  };
}

function draftResponse() {
  return {
    revisionId: '11111111-1111-1111-1111-111111111111',
    problemId: 1,
    revisionNumber: 1,
    status: 'Draft' as unknown as number,
    slug: 'double-value',
    title: 'Double Value',
    statementMarkdown: 'Return twice the input.',
    constraintsMarkdown: '- The result fits in Int32.',
    difficulty: 'Easy' as unknown as number,
    timeLimitMs: 1000,
    memoryLimitKb: 262144,
    samples: [{ input: '{"value":1}', expectedOutput: '2' }],
    definition: {
      schemaVersion: 1,
      executionMode: 'Function' as unknown as number,
      functionSignature: {
        className: 'Solution',
        methodName: 'solve',
        returnType: 'Int32' as unknown as number,
        parameters: [{ name: 'value', type: 'Int32' as unknown as number }],
      },
      handwrittenCases: [],
      generator: { language: 'csharp', sdkVersion: 1, source: '' },
      inputValidator: { language: 'csharp', sdkVersion: 1, source: '' },
      referenceSolution: { language: 'cpp17', source: '' },
      wrongSolutions: [],
    },
    updatedAt: '2026-07-25T00:00:00Z',
  };
}
