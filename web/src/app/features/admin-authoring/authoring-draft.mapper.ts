import type { ProblemDraftResponse } from '../../core/api/admin-generated/models/problem-draft-response';
import type { FunctionValueTypeName } from './data-access/authoring.models';

export type AuthoringStep = 'metadata' | 'signature' | 'cases' | 'sources' | 'review';

const VALUE_TYPES: readonly FunctionValueTypeName[] = [
  'Int32',
  'Int64',
  'Double',
  'Boolean',
  'String',
  'Int32Array',
  'Int64Array',
  'DoubleArray',
  'BooleanArray',
  'StringArray',
];

export function asDifficulty(value: unknown): 'Easy' | 'Medium' | 'Hard' {
  if (value === 'Hard' || value === 3) return 'Hard';
  if (value === 'Medium' || value === 2) return 'Medium';
  return 'Easy';
}

export function asValueType(value: unknown): FunctionValueTypeName {
  if (typeof value === 'string' && VALUE_TYPES.includes(value as FunctionValueTypeName))
    return value as FunctionValueTypeName;
  return VALUE_TYPES[typeof value === 'number' ? value : 0] ?? 'Int32';
}

export function sourceOrDefault(value: { source?: string } | undefined, fallback: string): string {
  return value?.source || fallback;
}

export function firstWrongSolution(
  values: readonly { name?: string; source?: string }[] | undefined,
  fallbackSource: string,
): {
  readonly name: string;
  readonly source: string;
} {
  const value = values?.[0];
  return {
    name: value?.name || 'adjacent-only',
    source: value?.source || fallbackSource,
  };
}

export function inferAuthoringStep(draft: ProblemDraftResponse): AuthoringStep {
  if (isReviewStatus(draft.status)) return 'review';
  const definition = draft.definition;
  if (!definition?.functionSignature?.className) return 'signature';
  if (!definition.handwrittenCases?.length) return 'cases';
  if (!definition.generator?.source || !definition.referenceSolution?.source) return 'sources';
  return 'review';
}

export function isGeneratingStatus(status: unknown): boolean {
  return status === 'Generating' || status === 1;
}

function isReviewStatus(status: unknown): boolean {
  return (
    isGeneratingStatus(status) ||
    status === 'Ready' ||
    status === 'Published' ||
    status === 2 ||
    status === 3
  );
}
