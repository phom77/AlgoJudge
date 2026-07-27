import { expect, test } from '@playwright/test';

import { registerAcceptanceUser, resetAcceptanceState } from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('manages problem availability and starts a new revision from the admin dashboard', async ({
  page,
}) => {
  await registerAcceptanceUser(page);
  await page.getByRole('link', { name: 'Admin' }).click();

  await expect(page.getByRole('heading', { name: 'Problem management' })).toBeVisible();
  await expect(page.getByText('Two Sum', { exact: true })).toBeVisible();
  const twoSumRow = page.getByRole('row').filter({ hasText: 'Two Sum' });
  await page.getByRole('button', { name: 'Archive' }).click();
  await expect(twoSumRow.getByRole('button', { name: 'Restore' })).toBeVisible();

  await twoSumRow.getByRole('button', { name: 'Restore' }).click();
  await expect(twoSumRow).toContainText('Published');
  await twoSumRow.getByRole('button', { name: 'Create revision' }).click();
  await expect(page).toHaveURL(/\/admin\/problems\/[0-9a-f-]{36}\/author$/);
  await expect(page.getByRole('heading', { name: 'Two Sum' })).toBeVisible();
});
