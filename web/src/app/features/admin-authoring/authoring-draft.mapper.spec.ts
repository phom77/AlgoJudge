import { describe, expect, it } from 'vitest';

import type { ProblemDraftResponse } from '../../core/api/admin-generated/models/problem-draft-response';
import { asValueType, inferAuthoringStep, isGeneratingStatus } from './authoring-draft.mapper';

describe('authoring draft mapper', () => {
  it('maps both runtime string enums and generated numeric enums', () => {
    expect(asValueType('Int64Array')).toBe('Int64Array');
    expect(asValueType(0)).toBe('Int32');
    expect(asValueType(9)).toBe('StringArray');
  });

  it('restores an in-flight revision at the review step', () => {
    const draft = {
      status: 'Generating',
      definition: {},
    } as unknown as ProblemDraftResponse;

    expect(inferAuthoringStep(draft)).toBe('review');
    expect(isGeneratingStatus(draft.status)).toBe(true);
  });
});
