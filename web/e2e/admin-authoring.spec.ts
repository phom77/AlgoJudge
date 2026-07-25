import { expect, test } from '@playwright/test';

import { registerAcceptanceUser, resetAcceptanceState } from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('authors, publishes, and judges both Accepted and Wrong Answer solutions', async ({
  page,
}) => {
  await registerAcceptanceUser(page);
  await page.getByRole('link', { name: 'Authoring' }).click();

  await page.getByLabel('Slug').fill('authored-two-sum');
  await page.getByLabel('Title').fill('Authored Two Sum');
  await page
    .getByLabel('Statement (Markdown)')
    .fill('Return the two indices whose values add to target.');
  await page.getByLabel('Constraints (Markdown)').fill('- 2 ≤ values.length ≤ 10,000');
  await page.getByRole('button', { name: 'Create draft' }).click();

  await expect(page.getByRole('heading', { name: 'Function signature' })).toBeVisible();
  await page.getByRole('button', { name: 'Save signature' }).click();
  await expect(page.getByRole('heading', { name: 'Handwritten testcases' })).toBeVisible();
  await page.getByRole('button', { name: 'Save cases' }).click();
  await expect(page.getByRole('heading', { name: 'Generation sources' })).toBeVisible();
  await page.getByRole('button', { name: 'Save sources' }).click();
  await expect(page.getByRole('heading', { name: 'Generate, review, publish' })).toBeVisible();

  await page.getByRole('button', { name: 'Generate suite' }).click();
  await expect(page.getByText('Safe testcase metadata preview')).toBeVisible({ timeout: 8_000 });
  await expect(page.locator('main')).toContainText('1000');
  await expect(page.locator('main')).toContainText('adversarial');
  await expect(page.locator('main')).not.toContainText('{"values"');
  await page.getByRole('button', { name: 'Publish problem' }).click();
  await expect(page.getByText('Published. The problem is now available')).toBeVisible();

  await page.goto('/problems/authored-two-sum');
  await expect(page.getByRole('heading', { name: 'Authored Two Sum' })).toBeVisible();
  await page.getByRole('tab', { name: 'Submit' }).click();
  await page.locator('aj-problem-execution-panel footer button.action').click();
  await expect(page.locator('aj-submission-result-panel').getByText('Accepted')).toBeVisible({
    timeout: 8_000,
  });

  await page.reload();
  const editor = page.locator('.monaco-editor').first();
  await expect(editor).toBeVisible();
  await editor.click();
  await page.keyboard.press('Control+A');
  await page.keyboard.type('// WRONG\nint main() { return 0; }');
  await page.getByRole('tab', { name: 'Submit' }).click();
  await page.locator('aj-problem-execution-panel footer button.action').click();
  await expect(page.locator('aj-submission-result-panel').getByText('Wrong Answer')).toBeVisible({
    timeout: 8_000,
  });
});
