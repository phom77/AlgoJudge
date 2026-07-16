const DEFAULT_RETURN_URL = '/problems';

export function normalizeReturnUrl(value: string | null): string {
  if (value === null || !value.startsWith('/') || value.startsWith('//') || value.includes('\\')) {
    return DEFAULT_RETURN_URL;
  }

  return value;
}
