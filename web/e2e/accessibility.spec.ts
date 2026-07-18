import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import type { Page } from '@playwright/test';

import {
  openWorkspace,
  registerAcceptanceUser,
  resetAcceptanceState,
} from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('has no automated WCAG A/AA violations across the MVP pages', async ({ page }) => {
  for (const route of ['/login', '/register', '/problems']) {
    await page.goto(route);
    await expectNoA11yViolations(page);
  }

  await registerAcceptanceUser(page);
  await expectNoA11yViolations(page);
  await openWorkspace(page);
  await expectNoA11yViolations(page);
  await page.goto('/submissions');
  await expect(page.getByRole('heading', { name: 'Submission history' })).toBeVisible();
  await expectNoA11yViolations(page);
});

test('keeps the workspace operable and accessible at a mobile viewport', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await registerAcceptanceUser(page);
  await openWorkspace(page, false);
  await expect(page.getByRole('button', { name: 'Description' })).toBeVisible();
  await page.getByRole('button', { name: 'Code' }).click();
  await expect(page.locator('.monaco-editor')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Run Code' })).toBeVisible();
  await page.getByRole('tab', { name: 'Submit' }).click();
  await expect(page.locator('aj-problem-execution-panel footer button.action')).toBeVisible();
  await expectNoA11yViolations(page);
});

async function expectNoA11yViolations(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
    .analyze();
  expect(results.violations, formatViolations(results.violations)).toEqual([]);
}

function formatViolations(violations: { id: string; help: string; nodes: unknown[] }[]): string {
  return violations
    .map((violation) => `${violation.id}: ${violation.help} (${violation.nodes.length} nodes)`)
    .join('\n');
}
