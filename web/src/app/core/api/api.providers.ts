import { makeEnvironmentProviders } from '@angular/core';
import type { EnvironmentProviders } from '@angular/core';

import { provideAlgoJudgeApiConfiguration } from './generated/algo-judge-api-configuration';

const DEFAULT_API_ROOT_URL = '';

export function provideAlgoJudgeApi(rootUrl = DEFAULT_API_ROOT_URL): EnvironmentProviders {
  return makeEnvironmentProviders([provideAlgoJudgeApiConfiguration(normalizeRootUrl(rootUrl))]);
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
