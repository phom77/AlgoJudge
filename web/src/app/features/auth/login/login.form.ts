import type { NonNullableFormBuilder } from '@angular/forms';
import { Validators } from '@angular/forms';

export function createLoginForm(builder: NonNullableFormBuilder) {
  return builder.group({
    userName: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(50)]],
    password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(100)]],
  });
}
