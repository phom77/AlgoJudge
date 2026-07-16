import { ChangeDetectionStrategy, Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';

import { AuthStore } from '../../../core/auth/auth.store';
import { normalizeReturnUrl } from '../../../core/auth/return-url';
import { controlError, serverFieldError } from '../auth-form-errors';
import { createRegisterForm } from './register.form';

type RegistrationField = 'userName' | 'email' | 'fullName' | 'password';

@Component({
  selector: 'aj-register-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.page.html',
  styleUrl: './register.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPage {
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  protected readonly authStore = inject(AuthStore);
  protected readonly returnUrl = normalizeReturnUrl(
    this.route.snapshot.queryParamMap.get('returnUrl'),
  );
  protected readonly form = createRegisterForm(inject(NonNullableFormBuilder));

  constructor() {
    this.authStore.clearProblem();
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.authStore.isSubmitting()) {
      return;
    }

    this.authStore
      .register(this.form.getRawValue())
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.router.navigateByUrl(this.returnUrl));
  }

  protected errorFor(field: RegistrationField, label: string): string | null {
    return (
      controlError(this.form.controls[field], label) ??
      serverFieldError(this.authStore.problem(), field)
    );
  }
}
