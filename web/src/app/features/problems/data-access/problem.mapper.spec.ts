import type { ProblemDetailResponse } from '../../../core/api/generated/models/problem-detail-response';
import { mapProblemDetail, mapProblemPage } from './problem.mapper';

describe('problem mapper', () => {
  it('isolates string enum responses from the generated numeric enum type', () => {
    const page = mapProblemPage({
      pageNumber: '1',
      pageSize: '20',
      totalCount: '1',
      totalPages: '1',
      items: [
        {
          id: '7',
          slug: 'two-sum',
          title: 'Two Sum',
          difficulty: 'Easy' as unknown as number,
          isSolved: null,
          tags: [{ slug: 'Array', name: 'Array' }],
        },
      ],
    });

    expect(page.items[0]).toMatchObject({ id: 7, difficulty: 'Easy', isSolved: null });
    expect(page.items[0]?.tags[0]?.slug).toBe('array');
  });

  it('orders public samples and never copies unknown hidden test properties', () => {
    const response = {
      id: 7,
      slug: 'two-sum',
      title: 'Two Sum',
      difficulty: 1,
      statementMarkdown: 'Statement',
      constraintsMarkdown: 'Constraints',
      samples: [
        { ordinal: 2, input: '2', expectedOutput: '4' },
        { ordinal: 1, input: '1', expectedOutput: '2' },
      ],
      hiddenInput: 'private',
    } as ProblemDetailResponse & { hiddenInput: string };

    const detail = mapProblemDetail(response);

    expect(detail.samples.map((sample) => sample.ordinal)).toEqual([1, 2]);
    expect('hiddenInput' in detail).toBe(false);
  });

  it('maps a public function signature without exposing its private adapter', () => {
    const response = {
      id: 7,
      slug: 'two-sum',
      title: 'Two Sum',
      difficulty: 1,
      executionMode: 1,
      functionSignature: {
        className: 'Solution',
        methodName: 'twoSum',
        returnType: 5,
        parameters: [
          { name: 'nums', type: 5 },
          { name: 'target', type: 0 },
        ],
        adapter: 'private',
      },
    } as unknown as ProblemDetailResponse;

    const detail = mapProblemDetail(response);

    expect(detail.executionMode).toBe('Function');
    expect(detail.functionSignature).toEqual({
      className: 'Solution',
      methodName: 'twoSum',
      returnType: 'Int32Array',
      parameters: [
        { name: 'nums', type: 'Int32Array' },
        { name: 'target', type: 'Int32' },
      ],
    });
    expect('adapter' in (detail.functionSignature ?? {})).toBe(false);
  });
});
