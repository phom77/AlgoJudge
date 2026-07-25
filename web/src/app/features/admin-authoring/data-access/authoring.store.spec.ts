import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';

import { AuthoringGateway } from './authoring.gateway';
import { AuthoringStore } from './authoring.store';

describe('AuthoringStore', () => {
  const get = vi.fn();
  const review = vi.fn();
  const generationStatus = vi.fn();

  beforeEach(() => {
    get.mockReset();
    review.mockReset();
    generationStatus.mockReset();
    TestBed.configureTestingModule({
      providers: [
        AuthoringStore,
        {
          provide: AuthoringGateway,
          useValue: {
            get,
            review,
            generationStatus,
          },
        },
      ],
    });
  });

  it('restores review and published state when reopening a published revision', () => {
    get.mockReturnValue(of(draftResponse('Published')));
    review.mockReturnValue(of(reviewResponse()));
    const store = TestBed.inject(AuthoringStore);

    store.load('11111111-1111-1111-1111-111111111111').subscribe();

    expect(review).toHaveBeenCalledOnce();
    expect(store.review()?.testCaseCount).toBe(85);
    expect(store.published()).toBe(true);
    expect(store.busy()).toBe(false);
  });

  it('keeps a ready revision publishable after restoring its review', () => {
    get.mockReturnValue(of(draftResponse('Ready')));
    review.mockReturnValue(of(reviewResponse()));
    const store = TestBed.inject(AuthoringStore);

    store.load('11111111-1111-1111-1111-111111111111').subscribe();

    expect(store.review()?.survivingWrongSolutions).toEqual([]);
    expect(store.published()).toBe(false);
  });

  it('resumes polling an existing generation without enqueuing another job', async () => {
    generationStatus.mockReturnValue(
      of({
        jobId: '22222222-2222-2222-2222-222222222222',
        revisionId: '11111111-1111-1111-1111-111111111111',
        jobStatus: 'Succeeded' as unknown as number,
        revisionStatus: 'Ready' as unknown as number,
        attemptCount: 1,
        createdAt: '2026-07-25T00:00:00Z',
        finishedAt: '2026-07-25T00:00:01Z',
      }),
    );
    review.mockReturnValue(of(reviewResponse()));
    const store = TestBed.inject(AuthoringStore);

    const result = await firstValueFrom(
      store.resumeGenerationAndReview('11111111-1111-1111-1111-111111111111'),
    );

    expect(generationStatus).toHaveBeenCalledOnce();
    expect(result?.testCaseCount).toBe(85);
    expect(store.busy()).toBe(false);
  });
});

function draftResponse(status: 'Ready' | 'Published') {
  return {
    revisionId: '11111111-1111-1111-1111-111111111111',
    problemId: 1,
    revisionNumber: 1,
    status: status as unknown as number,
    slug: 'two-sum',
    title: 'Two Sum',
    statementMarkdown: 'Return two indices.',
    constraintsMarkdown: '- Exactly one answer exists.',
    difficulty: 'Easy' as unknown as number,
    timeLimitMs: 1000,
    memoryLimitKb: 262144,
    samples: [{ input: '{"values":[2,7],"target":9}', expectedOutput: '[0,1]' }],
    definition: {
      schemaVersion: 1,
      executionMode: 'Function' as unknown as number,
      functionSignature: {
        className: 'Solution',
        methodName: 'twoSum',
        returnType: 'Int32Array' as unknown as number,
        parameters: [
          { name: 'values', type: 'Int32Array' as unknown as number },
          { name: 'target', type: 'Int32' as unknown as number },
        ],
      },
      handwrittenCases: [],
      generator: { language: 'csharp', sdkVersion: 1, source: 'generator' },
      inputValidator: { language: 'csharp', sdkVersion: 1, source: 'validator' },
      referenceSolution: { language: 'cpp17', source: 'reference' },
      wrongSolutions: [],
    },
    updatedAt: '2026-07-25T00:00:00Z',
  };
}

function reviewResponse() {
  return {
    revisionId: '11111111-1111-1111-1111-111111111111',
    suiteSha256: 'a'.repeat(64),
    testCaseCount: 85,
    casesByGroup: { handwritten: 1, edge: 12, random: 60, adversarial: 8, stress: 4 },
    wrongSolutionCount: 1,
    killedCaseCountByWrongSolution: { 'adjacent-only': 80 },
    survivingWrongSolutions: [],
    toolchain: 'generator-sdk-v1',
    casePreview: [],
    isCasePreviewTruncated: false,
  };
}
