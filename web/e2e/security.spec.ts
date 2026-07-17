import { expect, test } from '@playwright/test';

import {
  openWorkspace,
  registerAcceptanceUser,
  resetAcceptanceState,
} from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('enforces production CSP, Trusted Types, and baseline security headers', async ({ page }) => {
  const response = await page.goto('/problems');
  const headers = response?.headers() ?? {};
  const csp = headers['content-security-policy'] ?? '';

  expect(csp).toContain("frame-ancestors 'none'");
  expect(csp).toContain("object-src 'none'");
  expect(csp).toContain("script-src 'self'");
  expect(csp).not.toContain("'unsafe-eval'");
  expect(csp).toContain('trusted-types angular angular#bundler algojudge-monaco');
  expect(csp).toContain("require-trusted-types-for 'script'");
  expect(headers['x-content-type-options']).toBe('nosniff');
  expect(headers['x-frame-options']).toBe('DENY');
  expect(headers['referrer-policy']).toBe('strict-origin-when-cross-origin');

  const trustedTypesBlockedUnsafeSink = await page.evaluate(() => {
    try {
      document.createElement('div').innerHTML = '<strong>unsafe</strong>';
      return false;
    } catch {
      return true;
    }
  });
  expect(trustedTypesBlockedUnsafeSink).toBe(true);
});

test('loads lazy Angular routes and Monaco without CSP or Trusted Types violations', async ({
  page,
}) => {
  const browserErrors: string[] = [];
  page.on('console', (message) => {
    const text = message.text();
    if (
      message.type() === 'error' &&
      /(Content Security Policy|Trusted Types|TrustedHTML|TrustedScript)/i.test(text)
    ) {
      const location = message.location();
      browserErrors.push(`${text} (${location.url}:${location.lineNumber})`);
    }
  });
  page.on('pageerror', (error) => browserErrors.push(error.message));

  await registerAcceptanceUser(page);
  await openWorkspace(page);
  await expect(page.locator('.monaco-editor')).toBeVisible();
  await expect(page.getByText('Basic editor active')).toHaveCount(0);
  await expect(page.locator('link[data-algojudge-monaco-styles]')).toHaveCount(1);

  const imeTextAreaPosition = await page
    .locator('.monaco-editor .ime-text-area')
    .evaluate((element) => getComputedStyle(element).position);
  expect(imeTextAreaPosition).toBe('absolute');
  expect(browserErrors).toEqual([]);
});
