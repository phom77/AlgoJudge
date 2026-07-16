import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import type { ApiProblem } from '../../../core/error/api-problem';
import { LoginPage } from './login.page';

describe('LoginPage', () => {
  const problem = signal<ApiProblem | null>(null);
  const isSubmitting = signal(false);
  const login = vi.fn();
  const clearProblem = vi.fn();
  let fixture: ComponentFixture<LoginPage>;

  beforeEach(async () => {
    problem.set(null);
    isSubmitting.set(false);
    login.mockReset();
    clearProblem.mockReset();
    login.mockReturnValue(of(undefined));

    await TestBed.configureTestingModule({
      imports: [LoginPage],
      providers: [
        provideRouter([]),
        {
          provide: AuthStore,
          useValue: { problem, isSubmitting, login, clearProblem },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();
  });

  it('blocks invalid submission and renders accessible field errors', () => {
    submitForm(fixture);

    expect(login).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Username is required.');
    expect(fixture.nativeElement.textContent).toContain('Password is required.');
  });

  it('renders backend Problem Details without exposing trace metadata', () => {
    problem.set(apiProblem('Invalid credentials.'));
    fixture.detectChanges();
    const alert = fixture.nativeElement.querySelector('[role="alert"]') as HTMLElement;

    expect(alert.textContent).toContain('Sign in failed.');
    expect(alert.textContent).toContain('Invalid credentials.');
    expect(alert.textContent).not.toContain('trace-private');
  });

  it('disables the submit button while authentication is in progress', () => {
    isSubmitting.set(true);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;

    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Signing in');
  });
});

function submitForm(fixture: ComponentFixture<LoginPage>): void {
  const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
  form.dispatchEvent(new Event('submit'));
  fixture.detectChanges();
}

function apiProblem(detail: string): ApiProblem {
  return {
    status: 401,
    code: 'authentication',
    title: 'Sign in failed.',
    detail,
    type: 'urn:algojudge:error:authentication',
    instance: '/api/auth/login',
    traceId: 'trace-private',
    validationErrors: {},
    retryAfterSeconds: null,
  };
}
