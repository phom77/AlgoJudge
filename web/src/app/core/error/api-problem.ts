export const API_PROBLEM_CODES = [
  'validation',
  'authentication',
  'forbidden',
  'csrf',
  'not-found',
  'conflict',
  'rate-limit',
  'internal',
  'network',
  'unknown',
] as const;

export type ApiProblemCode = (typeof API_PROBLEM_CODES)[number];

export interface ApiProblem {
  readonly status: number;
  readonly code: ApiProblemCode;
  readonly title: string;
  readonly detail: string | null;
  readonly type: string | null;
  readonly instance: string | null;
  readonly traceId: string | null;
  readonly validationErrors: Readonly<Record<string, readonly string[]>>;
  readonly retryAfterSeconds: number | null;
}

export function isApiProblem(value: unknown): value is ApiProblem {
  if (typeof value !== 'object' || value === null || !('code' in value)) {
    return false;
  }

  return API_PROBLEM_CODES.some((code) => code === value.code);
}

export function createUnknownApiProblem(): ApiProblem {
  return {
    status: 0,
    code: 'unknown',
    title: 'An unexpected error occurred.',
    detail: null,
    type: null,
    instance: null,
    traceId: null,
    validationErrors: {},
    retryAfterSeconds: null,
  };
}
