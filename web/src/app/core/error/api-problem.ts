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
