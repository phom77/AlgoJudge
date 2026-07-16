import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthStore } from '../../../core/auth/auth.store';
import { normalizeReturnUrl } from '../../../core/auth/return-url';
import { controlError, serverFieldError } from '../auth-form-errors';
import { createLoginForm } from './login.form';

@Component({
  selector: 'aj-login-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.page.html',
  styleUrl: './login.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LoginPage {
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly authStore = inject(AuthStore);
  protected readonly returnUrl = normalizeReturnUrl(
    this.route.snapshot.queryParamMap.get('returnUrl'),
  );
  protected readonly form = createLoginForm(inject(NonNullableFormBuilder));

  constructor() {
    this.authStore.clearProblem();
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.authStore.isSubmitting()) {
      return;
    }

    this.authStore
      .login(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.router.navigateByUrl(this.returnUrl));
  }

  protected errorFor(field: 'userName' | 'password', label: string): string | null {
    return (
      controlError(this.form.controls[field], label) ??
      serverFieldError(this.authStore.problem(), field)
    );
  }
}
