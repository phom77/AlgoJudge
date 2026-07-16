import { inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { AuthStore } from './auth.store';

export function initializeAuthSession(): Observable<void> {
  return inject(AuthStore).restoreSession();
}
