import { expect, test } from '@playwright/test';

import {
  registerAcceptanceUser,
  registerRegularAcceptanceUser,
  resetAcceptanceState,
} from './support/acceptance-helpers';

const batchId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('reviews, retries, resumes, and selectively publishes a 100-problem batch', async ({
  page,
}) => {
  await registerAcceptanceUser(page);
  await page.getByRole('link', { name: 'Admin' }).click();
  await page.getByRole('link', { name: 'Content batches' }).click();

  await expect(page).toHaveURL(/\/admin\/content-batches$/);
  const batchRow = page.getByRole('row').filter({ hasText: 'acceptance-100/catalog.json' });
  await expect(batchRow).toContainText('90 ready');
  await expect(batchRow).toContainText('7 failed');
  await expect(batchRow).toContainText('3 skipped');
  await batchRow.getByRole('link', { name: 'View batch' }).click();

  await expect(page).toHaveURL(new RegExp(`/admin/content-batches/${batchId}$`));
  await expect(page.getByText('Partial failure:', { exact: false })).toBeVisible();
  await page.getByPlaceholder('Slug or title').fill('override generator');
  await expect(page.getByText('Override Generator', { exact: true })).toBeVisible();
  await expect(page.getByText('Override Validator', { exact: true })).toHaveCount(0);

  await page.getByPlaceholder('Slug or title').fill('');
  await page.getByLabel('Status').selectOption('failed');
  await expect(page.locator('tbody tr')).toHaveCount(7);
  const compileFailure = page.getByRole('row').filter({ hasText: 'Intentional Compile Failure' });
  await expect(compileFailure).toContainText('compile_error');
  await compileFailure.getByRole('button', { name: 'Retry' }).click();
  await expect(compileFailure).toHaveCount(0);

  await page.getByLabel('Status').selectOption('all');
  await page.getByPlaceholder('Slug or title').fill('invalid path');
  await expect(page.getByText('invalid_path', { exact: true })).toBeVisible();
  await expect(page.getByText('unsafe or invalid path', { exact: false })).toBeVisible();
  await page.getByPlaceholder('Slug or title').fill('problem-093');
  await expect(page.getByText('worker_unavailable', { exact: true })).toBeVisible();
  await expect(
    page.getByText('generation attempt was interrupted', { exact: false }),
  ).toBeVisible();

  await page.getByPlaceholder('Slug or title').fill('');
  await page.getByLabel('Status').selectOption('ready');
  await page.getByLabel('Select template-only for publish').check();
  await page.getByLabel('Select override-generator for publish').check();
  page.once('dialog', (dialog) => dialog.accept());
  await page.getByRole('button', { name: 'Publish selected (2)' }).click();
  await expect(page.getByText('Published', { exact: true }).first()).toBeVisible();

  const state = await (await page.request.get('/__e2e/content-batch-state')).json();
  expect(state.batchRetryRequests).toBe(1);
  expect(state.publishedBatchRevisionIds).toEqual([
    '20000000-0000-0000-0000-000000000001',
    '20000000-0000-0000-0000-000000000002',
  ]);
  expect(state.batchUniqueRevisionCount).toBe(state.batchRevisionCount);

  const publicCatalogue = await (
    await page.request.get('/api/problems?Search=Template&PageNumber=1&PageSize=20')
  ).json();
  expect(publicCatalogue.items.map((item: { slug: string }) => item.slug)).toEqual([
    'template-only',
  ]);

  await page.request.post('/__e2e/content-batch-worker-restart');
  await page.reload();
  await expect(page.getByRole('button', { name: 'Resume batch' })).toBeVisible();
  await page.getByRole('button', { name: 'Resume batch' }).click();
  await expect(page.getByText('Ready for review', { exact: true })).toBeVisible();
  const resumedState = await (await page.request.get('/__e2e/content-batch-state')).json();
  expect(resumedState.batchResumeRequests).toBe(1);

  const detailPayload = await (
    await page.request.get(`/api/internal/admin/content-batches/${batchId}`)
  ).text();
  expect(detailPayload).not.toContain('private-source');
  expect(detailPayload).not.toContain('hidden-input');
  expect(detailPayload).not.toContain('hidden-output');
});

test('keeps a regular user out of content batch administration', async ({ page }) => {
  await registerRegularAcceptanceUser(page);

  await page.goto('/admin/content-batches');

  await expect(page).toHaveURL(/\/forbidden$/);
  await expect(page.getByRole('heading', { name: 'This area is restricted' })).toBeVisible();
});
