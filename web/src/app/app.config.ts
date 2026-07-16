import { provideHttpClient, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import type { ApplicationConfig } from '@angular/core';
import { provideRouter } from '@angular/router';

import { routes } from './app.routes';
import { provideAlgoJudgeApi } from './core/api/api.providers';
import { initializeAuthSession } from './core/auth/auth.initializer';
import { authRefreshInterceptor } from './core/auth/auth-refresh.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(
      withXsrfConfiguration({
        cookieName: 'XSRF-TOKEN',
        headerName: 'X-XSRF-TOKEN',
      }),
      withInterceptors([authRefreshInterceptor]),
    ),
    provideAlgoJudgeApi(),
    provideAppInitializer(initializeAuthSession),
    provideRouter(routes),
  ],
};
