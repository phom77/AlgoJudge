import type { AbstractControl } from '@angular/forms';

import type { ApiProblem } from '../../core/error/api-problem';

export function controlError(control: AbstractControl, label: string): string | null {
  if (!control.touched || control.errors === null) {
    return null;
  }
  if (control.hasError('required')) {
    return `${label} is required.`;
  }
  if (control.hasError('minlength')) {
    return `${label} is too short.`;
  }
  if (control.hasError('maxlength')) {
    return `${label} is too long.`;
  }
  if (control.hasError('email')) {
    return 'Enter a valid email address.';
  }
  if (control.hasError('pattern')) {
    return 'Use only letters, numbers, and underscores.';
  }

  return `${label} is invalid.`;
}

export function serverFieldError(problem: ApiProblem | null, field: string): string | null {
  if (problem === null) {
    return null;
  }

  const matchedField = Object.keys(problem.validationErrors).find(
    (key) => key.toLowerCase() === field.toLowerCase(),
  );
  return matchedField === undefined ? null : (problem.validationErrors[matchedField]?.[0] ?? null);
}
