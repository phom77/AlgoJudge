import { expect } from '@playwright/test';
import type { APIRequestContext, Page } from '@playwright/test';

export const acceptanceUser = {
  userName: 'mvp_user',
  email: 'mvp@example.com',
  fullName: 'MVP Learner',
  password: 'Cpp17!acceptance',
};

export async function resetAcceptanceState(request: APIRequestContext): Promise<void> {
  const response = await request.post('/__e2e/reset');
  expect(response.ok()).toBe(true);
}

export async function registerAcceptanceUser(page: Page): Promise<void> {
  await page.goto('/register');
  await page.getByLabel('Username').fill(acceptanceUser.userName);
  await page.getByLabel('Email').fill(acceptanceUser.email);
  await page.getByLabel('Full name').fill(acceptanceUser.fullName);
  await page.getByLabel('Password').fill(acceptanceUser.password);
  await page.getByRole('button', { name: 'Create account' }).click();
  await expect(page).toHaveURL(/\/problems$/);
  await expect(page.getByRole('button', { name: 'Sign out' })).toBeVisible();
}

export async function openWorkspace(page: Page, expectEditorVisible = true): Promise<void> {
  await page.getByPlaceholder('Search problems').fill('Two Sum');
  await expect(page).toHaveURL(/search=Two(?:\+|%20)Sum/);
  await page.getByLabel('Difficulty').selectOption('Easy');
  await page.getByRole('button', { name: 'Array', exact: true }).click();
  await expect(page.getByRole('link', { name: 'Two Sum', exact: true })).toBeVisible();
  await page.getByRole('link', { name: 'Two Sum', exact: true }).click();
  await expect(page).toHaveURL(/\/problems\/two-sum$/);
  if (expectEditorVisible) await expect(page.locator('.monaco-editor')).toBeVisible();
}

export async function submitAcceptedSolution(page: Page): Promise<string> {
  const submitButton = page.locator('button.submit-button');
  await submitButton.click();
  await expect(submitButton).toBeDisabled();
  await expect(page.getByText('Your submission is queued for judging.')).toBeVisible();
  await expect(page.locator('aj-submission-result-panel').getByText('Accepted')).toBeVisible({
    timeout: 8_000,
  });
  await expect(page.getByText('Solved')).toBeVisible();
  const detailLink = page.getByRole('link', { name: 'View details' });
  const href = await detailLink.getAttribute('href');
  expect(href).toMatch(/^\/submissions\/[0-9a-f-]{36}$/);
  return href ?? '';
}
