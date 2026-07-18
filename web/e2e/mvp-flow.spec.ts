import { expect, test } from '@playwright/test';

import {
  acceptanceUser,
  openWorkspace,
  registerAcceptanceUser,
  resetAcceptanceState,
  submitAcceptedSolution,
} from './support/acceptance-helpers';

test.beforeEach(async ({ request }) => resetAcceptanceState(request));

test('registers, restores the session, submits C++17, and reviews solved history', async ({
  page,
  request,
}) => {
  let submissionCsrfHeader: string | undefined;
  page.on('request', (outgoingRequest) => {
    if (outgoingRequest.method() === 'POST' && outgoingRequest.url().endsWith('/api/submissions')) {
      submissionCsrfHeader = outgoingRequest.headers()['x-xsrf-token'];
    }
  });

  await registerAcceptanceUser(page);
  await openWorkspace(page);
  const detailUrl = await submitAcceptedSolution(page);

  expect(submissionCsrfHeader).toBe('e2e-xsrf-token');
  const state = await (await request.get('/__e2e/state')).json();
  expect(state).toEqual({
    createRequests: 1,
    submissions: 1,
    runCreateRequests: 0,
    runs: 0,
  });

  await page.reload();
  await expect(page.getByText(acceptanceUser.userName, { exact: true })).toBeVisible();
  await expect(page.getByText('Solved')).toBeVisible();

  await page.goto(detailUrl);
  await expect(page.getByRole('heading', { name: 'Problem #7' })).toBeVisible();
  await expect(page.locator('aj-submission-result-panel').getByText('Accepted')).toBeVisible();
  await expect(page.getByText('12 ms', { exact: true })).toBeVisible();
  await expect(page.getByText('2048 KB', { exact: true })).toBeVisible();
  await expect(page.locator('main')).not.toContainText('#include');

  await page.getByRole('link', { name: 'Submission history' }).click();
  await page.getByLabel('Problem ID').fill('7');
  await page.getByLabel('Verdict').selectOption('Accepted');
  await expect(page).toHaveURL(/problemId=7/);
  await expect(page).toHaveURL(/status=Accepted/);
  await expect(page.getByRole('row', { name: /Accepted Problem #7 C\+\+17 12 ms/ })).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();
  await expect(page.getByRole('link', { name: 'Sign in' })).toBeVisible();

  await page.goto('/login');
  await page.getByLabel('Username').fill(acceptanceUser.userName);
  await page.getByLabel('Password').fill(acceptanceUser.password);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await expect(page).toHaveURL(/\/problems$/);
  await expect(page.getByText(acceptanceUser.userName, { exact: true })).toBeVisible();

  await page.reload();
  await expect(page.getByText(acceptanceUser.userName, { exact: true })).toBeVisible();
  await page.getByRole('button', { name: 'Sign out' }).click();
  await page.goto('/submissions');
  await expect(page).toHaveURL(/\/login\?returnUrl=%2Fsubmissions$/);
});

test('runs custom stdin without creating history or solved progress', async ({ page, request }) => {
  let runRequest: { input?: string; sourceCode?: string } | undefined;
  let runCsrfHeader: string | undefined;
  page.on('request', (outgoingRequest) => {
    if (outgoingRequest.method() === 'POST' && outgoingRequest.url().endsWith('/runs')) {
      runRequest = outgoingRequest.postDataJSON();
      runCsrfHeader = outgoingRequest.headers()['x-xsrf-token'];
    }
  });

  await registerAcceptanceUser(page);
  await openWorkspace(page);
  await page.getByLabel('Custom stdin').fill('custom-output');
  await page.getByRole('button', { name: 'Run Code' }).click();

  const result = page.locator('aj-run-result-panel');
  await expect(result.getByText('Completed')).toBeVisible({ timeout: 8_000 });
  await expect(result.locator('pre')).toContainText('custom-output');
  await expect(page.getByText('Not solved')).toBeVisible();
  expect(runRequest).toMatchObject({ input: 'custom-output', sourceCode: expect.any(String) });
  expect(runCsrfHeader).toBe('e2e-xsrf-token');
  expect(await (await request.get('/__e2e/state')).json()).toEqual({
    createRequests: 0,
    submissions: 0,
    runCreateRequests: 1,
    runs: 1,
  });

  await page.goto('/submissions');
  await expect(page.getByRole('heading', { name: 'No submissions found' })).toBeVisible();
});

test('runs typed Function arguments and submits the system suite', async ({ page }) => {
  let runArguments: unknown;
  page.on('request', (outgoingRequest) => {
    if (outgoingRequest.method() === 'POST' && outgoingRequest.url().endsWith('/runs')) {
      runArguments = outgoingRequest.postDataJSON().arguments;
    }
  });

  await registerAcceptanceUser(page);
  await page.goto('/problems/double-function');
  await expect(page.getByRole('heading', { name: 'Double Function' })).toBeVisible();
  await expect(page.getByText('Solution.solve')).toBeVisible();
  await page.locator('aj-function-arguments-editor input').fill('21');
  await page.getByRole('button', { name: 'Run Code' }).click();

  const runResult = page.locator('aj-run-result-panel');
  await expect(runResult.getByText('Completed')).toBeVisible({ timeout: 8_000 });
  await expect(runResult.locator('pre')).toContainText('42');
  expect(runArguments).toEqual({ value: 21 });
  await expect(page.getByText('Not solved')).toBeVisible();

  await page.getByRole('tab', { name: 'Submit' }).click();
  await page.locator('aj-problem-execution-panel footer button.action').click();
  const submissionResult = page.locator('aj-submission-result-panel');
  await expect(submissionResult.getByText('Accepted')).toBeVisible({ timeout: 8_000 });
  await expect(submissionResult.getByText('v1')).toBeVisible();
  await expect(page.getByText('Solved', { exact: true })).toBeVisible();
});

test('surfaces CSRF rejection and allows an explicit retry', async ({ page }) => {
  await registerAcceptanceUser(page);
  await openWorkspace(page);

  await page.route(
    '**/api/submissions',
    async (route) => {
      await route.fulfill({
        status: 403,
        contentType: 'application/problem+json',
        body: JSON.stringify({
          status: 403,
          code: 'csrf',
          title: 'Antiforgery validation failed.',
          type: 'about:blank',
        }),
      });
    },
    { times: 1 },
  );

  await page.getByRole('tab', { name: 'Submit' }).click();
  const submitButton = page.locator('aj-problem-execution-panel footer button.action');
  await submitButton.click();
  await expect(page.locator('section.result[role="alert"]')).toContainText(
    'Antiforgery validation failed.',
  );
  await expect(submitButton).toBeEnabled();

  await page.unrouteAll({ behavior: 'wait' });
  await submitButton.click();
  await expect(page.locator('aj-submission-result-panel').getByText('Accepted')).toBeVisible({
    timeout: 8_000,
  });
});
