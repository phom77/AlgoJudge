import { TestBed } from '@angular/core/testing';

import type { ProblemDetail } from '../data-access/problem.models';
import { ProblemOverviewComponent } from './problem-overview.component';

describe('ProblemOverviewComponent', () => {
  it('renders limits, tags, and the public Function interface', async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemOverviewComponent],
    }).compileComponents();
    const fixture = TestBed.createComponent(ProblemOverviewComponent);
    fixture.componentRef.setInput('problem', functionProblem);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Array');
    expect(text).toContain('1000 ms');
    expect(text).toContain('256 MB');
    expect(text).toContain('Solution.solve');
    expect(text).toContain('vector<int> nums');
    expect(text).not.toContain('hidden');
  });
});

const functionProblem: ProblemDetail = {
  id: 7,
  slug: 'two-sum',
  title: 'Two Sum',
  difficulty: 'Easy',
  tags: [{ slug: 'array', name: 'Array' }],
  isSolved: false,
  statementMarkdown: 'Find two values.',
  constraintsMarkdown: '- Use C++17.',
  timeLimitMs: 1_000,
  memoryLimitKb: 262_144,
  judgeVersion: 1,
  executionMode: 'Function',
  functionSignature: {
    className: 'Solution',
    methodName: 'solve',
    returnType: 'Int32Array',
    parameters: [
      { name: 'nums', type: 'Int32Array' },
      { name: 'target', type: 'Int32' },
    ],
  },
  publishedAt: '2026-07-17T00:00:00Z',
  samples: [],
};
