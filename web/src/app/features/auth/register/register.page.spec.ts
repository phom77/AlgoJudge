import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import { RegisterPage } from './register.page';

describe('RegisterPage', () => {
  const problem = signal(null);
  const isSubmitting = signal(false);

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RegisterPage],
      providers: [
        provideRouter([]),
        {
          provide: AuthStore,
          useValue: {
            problem,
            isSubmitting,
            register: vi.fn(() => of(undefined)),
            clearProblem: vi.fn(),
          },
        },
      ],
    }).compileComponents();
  });

  it('renders fields with browser autofill semantics', () => {
    const fixture = TestBed.createComponent(RegisterPage);
    fixture.detectChanges();

    expect(autocomplete(fixture.nativeElement, '#register-user-name')).toBe('username');
    expect(autocomplete(fixture.nativeElement, '#register-email')).toBe('email');
    expect(autocomplete(fixture.nativeElement, '#register-full-name')).toBe('name');
    expect(autocomplete(fixture.nativeElement, '#register-password')).toBe('new-password');
  });
});

function autocomplete(root: HTMLElement, selector: string): string | null {
  return root.querySelector(selector)?.getAttribute('autocomplete') ?? null;
}
