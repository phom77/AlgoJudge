import type { ProblemDetail } from '../data-access/problem.models';
import { createCpp17Starter } from './cpp17-starter';

describe('createCpp17Starter', () => {
  it('creates a complete program for stdin/stdout problems', () => {
    const source = createCpp17Starter({
      ...baseProblem,
      executionMode: 'StdinStdout',
      functionSignature: null,
    });

    expect(source).toContain('int main()');
    expect(source).not.toContain('class Solution');
  });

  it('maps the public Function signature to a compilable class skeleton', () => {
    const source = createCpp17Starter({
      ...baseProblem,
      executionMode: 'Function',
      functionSignature: {
        className: 'Solution',
        methodName: 'solve',
        returnType: 'Int32Array',
        parameters: [
          { name: 'values', type: 'Int64Array' },
          { name: 'target', type: 'Int64' },
        ],
      },
    });

    expect(source).toContain('class Solution');
    expect(source).toContain('vector<int> solve(vector<long long> values, long long target)');
    expect(source).toContain('return {};');
    expect(source).not.toContain('int main()');
  });
});

const baseProblem: ProblemDetail = {
  id: 7,
  slug: 'two-sum',
  title: 'Two Sum',
  difficulty: 'Easy',
  tags: [],
  isSolved: false,
  statementMarkdown: 'Find two values.',
  constraintsMarkdown: '',
  timeLimitMs: 1_000,
  memoryLimitKb: 262_144,
  judgeVersion: 1,
  executionMode: 'StdinStdout',
  functionSignature: null,
  publishedAt: '2026-07-17T00:00:00Z',
  samples: [],
};
