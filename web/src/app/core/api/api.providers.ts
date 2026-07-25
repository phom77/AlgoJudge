import { makeEnvironmentProviders } from '@angular/core';
import type { EnvironmentProviders } from '@angular/core';

import { provideAlgoJudgeAdminApiConfiguration } from './admin-generated/algo-judge-admin-api-configuration';
import { provideAlgoJudgeApiConfiguration } from './generated/algo-judge-api-configuration';

const DEFAULT_API_ROOT_URL = '';

export function provideAlgoJudgeApi(rootUrl = DEFAULT_API_ROOT_URL): EnvironmentProviders {
  const normalizedRootUrl = normalizeRootUrl(rootUrl);
  return makeEnvironmentProviders([
    provideAlgoJudgeApiConfiguration(normalizedRootUrl),
    provideAlgoJudgeAdminApiConfiguration(normalizedRootUrl),
  ]);
}

function normalizeRootUrl(rootUrl: string): string {
  const value = rootUrl.trim();

  if (value === '' || value === '/') {
    return DEFAULT_API_ROOT_URL;
  }
  if (!value.startsWith('/') || value.startsWith('//')) {
    throw new Error('AlgoJudge API root URL must be a same-origin path.');
  }

  return value.replace(/\/+$/, '');
}
