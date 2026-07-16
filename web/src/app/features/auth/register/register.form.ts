import type { NonNullableFormBuilder } from '@angular/forms';
import { Validators } from '@angular/forms';

const USER_NAME_PATTERN = /^[a-zA-Z0-9_]+$/;

export function createRegisterForm(builder: NonNullableFormBuilder) {
  return builder.group({
    userName: [
      '',
      [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(50),
        Validators.pattern(USER_NAME_PATTERN),
      ],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
    fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
    password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(100)]],
  });
}
