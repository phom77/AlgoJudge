import { expect, test } from '@playwright/test';

import { registerRegularAcceptanceUser, resetAcceptanceState } from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('keeps regular users out of problem authoring', async ({ page }) => {
  await registerRegularAcceptanceUser(page);

  await expect(page.getByRole('link', { name: 'Admin' })).toHaveCount(0);
  await page.goto('/admin/problems/new');

  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole('heading', { name: 'This area is restricted' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Browse problems' })).toBeVisible();
});
