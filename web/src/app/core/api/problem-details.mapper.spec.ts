import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';

import { mapProblemDetails } from './problem-details.mapper';

describe('mapProblemDetails', () => {
  it('maps validation Problem Details and field errors', () => {
    const problem = mapProblemDetails(
      new HttpErrorResponse({
        status: 400,
        error: {
          status: '400',
          code: 'validation',
          title: 'The request is invalid.',
          detail: 'One or more validation errors occurred.',
          type: 'urn:algojudge:error:validation',
          instance: '/api/auth/register',
          traceId: 'trace-123',
          errors: {
            Email: ['Email is invalid.'],
          },
        },
      }),
    );

    expect(problem).toEqual({
      status: 400,
      code: 'validation',
      title: 'The request is invalid.',
      detail: 'One or more validation errors occurred.',
      type: 'urn:algojudge:error:validation',
      instance: '/api/auth/register',
      traceId: 'trace-123',
      validationErrors: { Email: ['Email is invalid.'] },
      retryAfterSeconds: null,
    });
  });

  it('preserves the csrf code instead of treating it as ordinary validation', () => {
    const problem = mapProblemDetails(
      new HttpErrorResponse({
        status: 400,
        error: { code: 'csrf', title: 'Request verification failed.' },
      }),
    );

    expect(problem.code).toBe('csrf');
    expect(problem.title).toBe('Request verification failed.');
  });

  it('uses status fallback and reads Retry-After seconds', () => {
    const problem = mapProblemDetails(
      new HttpErrorResponse({
        status: 429,
        headers: new HttpHeaders({ 'Retry-After': '7' }),
        error: { code: 'future-server-code' },
      }),
    );

    expect(problem.code).toBe('rate-limit');
    expect(problem.retryAfterSeconds).toBe(7);
  });

  it('returns a safe network error without exposing the underlying payload', () => {
    const problem = mapProblemDetails(
      new HttpErrorResponse({ status: 0, error: new Error('private browser detail') }),
    );

    expect(problem.code).toBe('network');
    expect(problem.detail).toBe('Unable to reach the server.');
    expect(JSON.stringify(problem)).not.toContain('private browser detail');
  });

  it('maps non-HTTP failures to a safe unknown problem', () => {
    const problem = mapProblemDetails(new Error('sensitive implementation detail'));

    expect(problem.code).toBe('unknown');
    expect(problem.detail).toBeNull();
  });
});
