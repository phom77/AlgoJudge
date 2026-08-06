import { expect, test } from '@playwright/test';

import {
  openWorkspace,
  registerAcceptanceUser,
  resetAcceptanceState,
} from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('keeps description, editor, and execution controls in one desktop workspace', async ({
  page,
}) => {
  await registerAcceptanceUser(page);
  await openWorkspace(page);

  const description = page.getByRole('region', { name: 'Problem description' });
  const editor = page.getByRole('region', { name: 'Solution editor' });
  await expect(description).toBeVisible();
  await expect(editor).toBeVisible();
  await expect(description.getByText('Time limit')).toBeVisible();
  await expect(page.getByRole('link', { name: 'My submissions' })).toHaveAttribute(
    'href',
    /problemSearch=Two(?:\+|%20)Sum/,
  );

  const descriptionBox = await description.boundingBox();
  const editorBox = await editor.boundingBox();
  expect(descriptionBox).not.toBeNull();
  expect(editorBox).not.toBeNull();
  expect(descriptionBox!.x).toBeLessThan(editorBox!.x);
  expect(Math.abs(descriptionBox!.y - editorBox!.y)).toBeLessThan(2);

  await page.getByRole('button', { name: 'Collapse console' }).click();
  await expect(page.getByLabel('Custom input')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Run Code' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Submit', exact: true })).toBeVisible();

  const testcaseTab = page.getByRole('tab', { name: 'Testcase' });
  await testcaseTab.focus();
  await testcaseTab.press('End');
  await expect(page.getByRole('tab', { name: 'Submit' })).toHaveAttribute('aria-selected', 'true');
});
