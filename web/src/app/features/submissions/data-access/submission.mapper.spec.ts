import type { SubmissionResponse } from '../../../core/api/generated/models/submission-response';
import { mapSubmission, mapSubmissionContent, mapSubmissionPage } from './submission.mapper';

describe('submission mapper', () => {
  it('accepts runtime string enums despite the generated numeric enum type', () => {
    const submission = mapSubmission({
      id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
      problemId: '4',
      problemTitle: 'Two Sum',
      problemSlug: 'two-sum',
      language: 'cpp17',
      status: 'Accepted' as unknown as number,
      executionTimeMs: '12',
      memoryUsedKb: '2048',
      createdAt: '2026-07-17T00:00:00Z',
      finishedAt: '2026-07-17T00:00:01Z',
    });

    expect(submission).toMatchObject({
      problemId: 4,
      problemTitle: 'Two Sum',
      problemSlug: 'two-sum',
      status: 'Accepted',
      executionTimeMs: 12,
      memoryUsedKb: 2048,
    });
  });

  it('maps numeric statuses and never copies unknown private fields', () => {
    const response = {
      id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
      problemId: 4,
      problemTitle: 'Two Sum',
      problemSlug: 'two-sum',
      language: 'cpp17',
      status: 4,
      createdAt: '2026-07-17T00:00:00Z',
      hiddenInput: 'private',
      sourceCode: 'secret',
    } as SubmissionResponse & { hiddenInput: string; sourceCode: string };

    const submission = mapSubmission(response);

    expect(submission.status).toBe('WrongAnswer');
    expect('hiddenInput' in submission).toBe(false);
    expect('sourceCode' in submission).toBe(false);
  });

  it('maps owner-only source and compiler diagnostics separately', () => {
    const content = mapSubmissionContent({
      sourceCode: 'int main() {\n  return 0;\n}',
      compileMessage: 'submission.cpp:1:1: error: test',
    });

    expect(content.sourceCode).toContain('\n  return 0;');
    expect(content.compileMessage).toContain('submission.cpp');
  });

  it('maps paged history metadata', () => {
    const page = mapSubmissionPage({
      pageNumber: '2',
      pageSize: '20',
      totalCount: '25',
      totalPages: '2',
    });

    expect(page).toMatchObject({ pageNumber: 2, pageSize: 20, totalCount: 25, totalPages: 2 });
  });
});
