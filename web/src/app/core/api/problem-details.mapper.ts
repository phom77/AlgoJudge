import { HttpErrorResponse } from '@angular/common/http';

import type { ApiProblem, ApiProblemCode } from '../error/api-problem';
import { API_PROBLEM_CODES } from '../error/api-problem';
import type { ApiProblemDetails } from './generated/models/api-problem-details';
import type { ApiValidationProblemDetails } from './generated/models/api-validation-problem-details';

type ProblemDetailsPayload = ApiProblemDetails & Pick<ApiValidationProblemDetails, 'errors'>;

const DEFAULT_TITLES: Readonly<Record<ApiProblemCode, string>> = {
  validation: 'The request is invalid.',
  authentication: 'Authentication is required.',
  forbidden: 'Access is forbidden.',
  csrf: 'Request verification failed.',
  'not-found': 'The requested resource was not found.',
  conflict: 'The request conflicts with the current state.',
  'rate-limit': 'Too many requests.',
  internal: 'The server could not complete the request.',
  network: 'Network error.',
  unknown: 'An unexpected error occurred.',
};

export function mapProblemDetails(error: unknown): ApiProblem {
  if (!(error instanceof HttpErrorResponse)) {
    return createFallbackProblem('unknown');
  }
  if (error.status === 0) {
    return createFallbackProblem('network');
  }

  const payload = readPayload(error.error);
  const status = readStatus(payload.status, error.status);
  const code = readProblemCode(payload.code, status);

  return {
    status,
    code,
    title: readText(payload.title) ?? DEFAULT_TITLES[code],
    detail: readText(payload.detail),
    type: readText(payload.type),
    instance: readText(payload.instance),
    traceId: readText(payload.traceId),
    validationErrors: readValidationErrors(payload.errors),
    retryAfterSeconds: parseRetryAfter(error.headers.get('Retry-After')),
  };
}

function readPayload(value: unknown): ProblemDetailsPayload {
  return isRecord(value) ? (value as ProblemDetailsPayload) : {};
}

function createFallbackProblem(code: 'network' | 'unknown'): ApiProblem {
  return {
    status: 0,
    code,
    title: DEFAULT_TITLES[code],
    detail: code === 'network' ? 'Unable to reach the server.' : null,
    type: null,
    instance: null,
    traceId: null,
    validationErrors: {},
    retryAfterSeconds: null,
  };
}

function readProblemCode(value: unknown, status: number): ApiProblemCode {
  if (typeof value === 'string' && API_PROBLEM_CODES.some((code) => code === value)) {
    return value as ApiProblemCode;
  }

  return statusToProblemCode(status);
}

function statusToProblemCode(status: number): ApiProblemCode {
  switch (status) {
    case 400:
      return 'validation';
    case 401:
      return 'authentication';
    case 403:
      return 'forbidden';
    case 404:
      return 'not-found';
    case 409:
      return 'conflict';
    case 429:
      return 'rate-limit';
    default:
      return status >= 500 ? 'internal' : 'unknown';
  }
}

function readStatus(value: unknown, fallback: number): number {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isInteger(parsed) && parsed >= 100 && parsed <= 599 ? parsed : fallback;
}

function readText(value: unknown): string | null {
  return typeof value === 'string' && value.trim().length > 0 ? value.trim() : null;
}

function readValidationErrors(value: unknown): Readonly<Record<string, readonly string[]>> {
  if (!isRecord(value)) {
    return {};
  }

  const errors: Record<string, readonly string[]> = {};
  for (const [field, messages] of Object.entries(value)) {
    if (Array.isArray(messages)) {
      errors[field] = messages.filter((message): message is string => typeof message === 'string');
    }
  }
  return errors;
}

function parseRetryAfter(value: string | null): number | null {
  if (value === null) {
    return null;
  }

  const seconds = Number(value);
  if (Number.isFinite(seconds) && seconds >= 0) {
    return Math.ceil(seconds);
  }

  const retryAt = Date.parse(value);
  return Number.isNaN(retryAt) ? null : Math.max(0, Math.ceil((retryAt - Date.now()) / 1_000));
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
