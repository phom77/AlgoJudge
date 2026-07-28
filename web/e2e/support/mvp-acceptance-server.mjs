import { createServer } from 'node:http';
import { readFile, stat } from 'node:fs/promises';
import { extname, resolve, sep } from 'node:path';
import { randomBytes, randomUUID, scryptSync, timingSafeEqual } from 'node:crypto';

const port = Number(process.env['E2E_PORT'] ?? 4300);
const root = resolve('dist/algojudge-web/browser');
const securityHeaders = JSON.parse(await readFile(resolve('config/security-headers.json'), 'utf8'));

const problem = {
  id: 7,
  slug: 'two-sum',
  title: 'Two Sum',
  difficulty: 'Easy',
  tags: [
    { slug: 'array', name: 'Array' },
    { slug: 'hash-table', name: 'Hash Table' },
  ],
};

const functionProblem = {
  id: 8,
  slug: 'double-function',
  title: 'Double Function',
  difficulty: 'Easy',
  tags: [{ slug: 'math', name: 'Math' }],
};

let state = createState();

const server = createServer(async (request, response) => {
  try {
    setSecurityHeaders(response);
    const url = new URL(request.url ?? '/', `http://${request.headers.host ?? '127.0.0.1'}`);

    if (url.pathname === '/__e2e/reset' && request.method === 'POST') {
      state = createState();
      return json(response, 200, { reset: true });
    }
    if (url.pathname === '/__e2e/state' && request.method === 'GET') {
      return json(response, 200, {
        createRequests: state.createRequests,
        submissions: state.submissions.size,
        runCreateRequests: state.runCreateRequests,
        runs: state.runs.size,
      });
    }
    if (url.pathname === '/__e2e/content-batch-state' && request.method === 'GET') {
      const revisionIds = state.contentBatch.items.map((item) => item.revisionId).filter(Boolean);
      return json(response, 200, {
        batchRetryRequests: state.batchRetryRequests,
        batchResumeRequests: state.batchResumeRequests,
        publishedBatchRevisionIds: state.publishedBatchRevisionIds,
        batchRevisionCount: revisionIds.length,
        batchUniqueRevisionCount: new Set(revisionIds).size,
      });
    }
    if (url.pathname === '/__e2e/content-batch-worker-restart' && request.method === 'POST') {
      const item = state.contentBatch.items.find((candidate) => candidate.status === 2);
      item.status = 1;
      state.contentBatch.status = 2;
      refreshBatchCounts(state.contentBatch);
      return json(response, 200, { restarted: true });
    }
    if (url.pathname.startsWith('/api/')) {
      return await handleApi(request, response, url);
    }
    return await serveStatic(request, response, url.pathname);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Unknown acceptance server error.';
    return problemDetails(
      response,
      500,
      'acceptance-server',
      'Acceptance server failure.',
      message,
    );
  }
});

server.listen(port, '127.0.0.1', () => {
  process.stdout.write(`AlgoJudge acceptance server listening on http://127.0.0.1:${port}\n`);
});

for (const signal of ['SIGINT', 'SIGTERM']) {
  process.on(signal, () => server.close(() => process.exit(0)));
}

function createState() {
  return {
    users: new Map(),
    sessions: new Map(),
    submissions: new Map(),
    runs: new Map(),
    createRequests: 0,
    runCreateRequests: 0,
    authoringDraft: null,
    adminProblems: [
      {
        id: 7,
        slug: 'two-sum',
        title: 'Two Sum',
        difficulty: 1,
        status: 2,
        judgeVersion: 1,
        latestRevisionId: '77777777-7777-7777-7777-777777777777',
        latestRevisionStatus: 3,
        publishedAt: '2026-07-17T00:00:00Z',
        updatedAt: '2026-07-17T00:00:00Z',
      },
      {
        id: 8,
        slug: 'double-function',
        title: 'Double Function',
        difficulty: 1,
        status: 1,
        judgeVersion: 1,
        latestRevisionId: '88888888-8888-8888-8888-888888888888',
        latestRevisionStatus: 2,
        publishedAt: null,
        updatedAt: '2026-07-21T00:00:00Z',
      },
      {
        id: 10,
        slug: 'retired-array-sum',
        title: 'Retired Array Sum',
        difficulty: 2,
        status: 3,
        judgeVersion: 1,
        latestRevisionId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        latestRevisionStatus: 3,
        publishedAt: '2026-07-10T00:00:00Z',
        updatedAt: '2026-07-20T00:00:00Z',
      },
    ],
    contentBatch: createScaleContentBatch(),
    batchRetryRequests: 0,
    batchResumeRequests: 0,
    publishedBatchRevisionIds: [],
    generationPolls: 0,
  };
}

async function handleApi(request, response, url) {
  const cookies = parseCookies(request.headers.cookie);
  const userName = state.sessions.get(cookies['algojudge_session']);

  if (url.pathname === '/api/auth/csrf' && request.method === 'GET') {
    response.setHeader('Set-Cookie', 'XSRF-TOKEN=e2e-xsrf-token; Path=/; SameSite=Strict');
    response.writeHead(204);
    return response.end();
  }

  if (url.pathname === '/api/auth/session' && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    return json(response, 200, authResponse(state.users.get(userName)));
  }

  if (url.pathname === '/api/auth/register' && request.method === 'POST') {
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    const body = await readJson(request);
    const user = createUser(body);
    state.users.set(user.userName, user);
    const sessionId = randomUUID();
    state.sessions.set(sessionId, user.userName);
    setSessionCookie(response, sessionId);
    return json(response, 201, authResponse(user));
  }

  if (url.pathname === '/api/auth/login' && request.method === 'POST') {
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    const body = await readJson(request);
    const user = state.users.get(body.userName);
    if (!user || !passwordMatches(body.password, user)) return authenticationProblem(response);
    const sessionId = randomUUID();
    state.sessions.set(sessionId, user.userName);
    setSessionCookie(response, sessionId);
    return json(response, 200, authResponse(user));
  }

  if (url.pathname === '/api/auth/refresh' && request.method === 'POST') {
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    if (!userName) return authenticationProblem(response);
    return json(response, 200, authResponse(state.users.get(userName)));
  }

  if (url.pathname === '/api/auth/revoke' && request.method === 'POST') {
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    state.sessions.delete(cookies['algojudge_session']);
    response.setHeader(
      'Set-Cookie',
      'algojudge_session=; Path=/; HttpOnly; SameSite=Strict; Max-Age=0',
    );
    response.writeHead(204);
    return response.end();
  }

  if (url.pathname === '/api/internal/admin/content-batches' && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    const batch = state.contentBatch;
    return json(
      response,
      200,
      page(
        [
          {
            id: batch.id,
            catalogName: batch.catalogName,
            status: batch.status,
            createdByUserId: batch.createdByUserId,
            counts: batch.counts,
            createdAt: batch.createdAt,
            updatedAt: batch.updatedAt,
          },
        ],
        url,
      ),
    );
  }

  const contentBatchMatch =
    /^\/api\/internal\/admin\/content-batches\/([0-9a-f-]+)(?:\/(start|resume|retry|publish))?$/i.exec(
      url.pathname,
    );
  if (contentBatchMatch) {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    if (contentBatchMatch[1] !== state.contentBatch.id)
      return problemDetails(response, 404, 'not-found', 'Content batch not found.');
    const action = contentBatchMatch[2];
    if (!action && request.method === 'GET') return json(response, 200, state.contentBatch);
    if (request.method !== 'POST') return problemDetails(response, 405, 'method', 'Method denied.');
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    if (action === 'start' || action === 'resume') {
      if (action === 'resume') state.batchResumeRequests += 1;
      for (const item of state.contentBatch.items) {
        if (item.status === 0 || item.status === 1 || item.status === 5) item.status = 2;
      }
      state.contentBatch.status = 3;
      refreshBatchCounts(state.contentBatch);
      state.contentBatch.updatedAt = new Date().toISOString();
      state.contentBatch.auditEntries.push(batchAudit(`batch.${action}`, 'completed'));
      return json(response, 202, state.contentBatch);
    }
    if (action === 'retry') {
      const body = await readJson(request);
      const itemIds = Array.isArray(body.itemIds) ? body.itemIds : [];
      state.batchRetryRequests += 1;
      for (const item of state.contentBatch.items) {
        if (
          itemIds.includes(item.id) &&
          item.status === 4 &&
          item.revisionId &&
          !['duplicate_slug', 'invalid_path'].includes(item.safeFailureCategory)
        ) {
          item.status = 2;
          item.safeFailureCategory = null;
          item.safeFailureMessage = null;
          item.updatedAt = new Date().toISOString();
        }
      }
      refreshBatchCounts(state.contentBatch);
      state.contentBatch.auditEntries.push(batchAudit('batch.retry', 'completed'));
      return json(response, 202, state.contentBatch);
    }
    if (action === 'publish') {
      const body = await readJson(request);
      const revisionIds = Array.isArray(body.revisionIds) ? body.revisionIds : [];
      for (const revisionId of revisionIds) {
        const item = state.contentBatch.items.find(
          (candidate) => candidate.revisionId === revisionId && candidate.status === 2,
        );
        if (!item)
          return problemDetails(
            response,
            409,
            'conflict',
            'Only Ready revisions can be published.',
          );
        item.status = 3;
        item.updatedAt = new Date().toISOString();
        state.publishedBatchRevisionIds.push(revisionId);
      }
      refreshBatchCounts(state.contentBatch);
      state.contentBatch.auditEntries.push(batchAudit('batch.publish', 'completed'));
      return json(response, 200, state.contentBatch);
    }
  }

  if (url.pathname === '/api/internal/admin/problems' && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    const search = (url.searchParams.get('Search') ?? '').trim().toLowerCase();
    const status = url.searchParams.get('Status');
    const items = state.adminProblems.filter(
      (item) =>
        (!search || item.title.toLowerCase().includes(search) || item.slug.includes(search)) &&
        (!status || item.status === Number(status)),
    );
    return json(response, 200, page(items, url));
  }

  const managementRevision = /^\/api\/internal\/admin\/problems\/(\d+)\/revisions$/i.exec(
    url.pathname,
  );
  if (managementRevision && request.method === 'POST') {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    const managed = state.adminProblems.find((item) => item.id === Number(managementRevision[1]));
    if (!managed) return problemDetails(response, 404, 'not-found', 'Problem not found.');
    state.authoringDraft = managementDraft(managed);
    managed.latestRevisionId = state.authoringDraft.revisionId;
    managed.latestRevisionStatus = 0;
    managed.updatedAt = '2026-07-27T00:00:00Z';
    return json(response, 201, state.authoringDraft);
  }

  const managementTransition = /^\/api\/internal\/admin\/problems\/(\d+)\/(archive|restore)$/i.exec(
    url.pathname,
  );
  if (managementTransition && request.method === 'POST') {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    const managed = state.adminProblems.find((item) => item.id === Number(managementTransition[1]));
    if (!managed) return problemDetails(response, 404, 'not-found', 'Problem not found.');
    managed.status = managementTransition[2] === 'archive' ? 3 : 2;
    managed.updatedAt = '2026-07-27T00:00:00Z';
    response.writeHead(204);
    return response.end();
  }

  if (url.pathname === '/api/internal/admin/problem-drafts' && request.method === 'POST') {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    const body = await readJson(request);
    state.authoringDraft = {
      revisionId: randomUUID(),
      problemId: 9,
      revisionNumber: 1,
      status: 'Draft',
      ...body,
      definition: {
        schemaVersion: 1,
        executionMode: 'Function',
        functionSignature: {},
        handwrittenCases: [],
        generator: { language: 'csharp', sdkVersion: 1, source: '' },
        inputValidator: { language: 'csharp', sdkVersion: 1, source: '' },
        referenceSolution: { language: 'cpp17', source: '' },
        wrongSolutions: [],
        qualityPolicy: {
          minimumTestCaseCount: 1,
          minimumCasesByGroup: [{ group: 'handwritten', minimumCaseCount: 1 }],
          requireEachDeclaredWrongSolutionKilled: true,
        },
      },
      updatedAt: '2026-07-22T00:00:00Z',
    };
    state.adminProblems.push({
      id: state.authoringDraft.problemId,
      slug: state.authoringDraft.slug,
      title: state.authoringDraft.title,
      difficulty: state.authoringDraft.difficulty,
      status: 1,
      judgeVersion: 1,
      latestRevisionId: state.authoringDraft.revisionId,
      latestRevisionStatus: 0,
      publishedAt: null,
      updatedAt: state.authoringDraft.updatedAt,
    });
    return json(response, 201, state.authoringDraft);
  }

  const draftMatch = /^\/api\/internal\/admin\/problem-drafts\/([0-9a-f-]+)$/i.exec(url.pathname);
  if (draftMatch && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    return state.authoringDraft
      ? json(response, 200, state.authoringDraft)
      : problemDetails(response, 404, 'not-found', 'Draft not found.');
  }

  const authoringAction =
    /^\/api\/internal\/admin\/problem-drafts\/([0-9a-f-]+)\/(metadata|signature|handwritten-cases|sources|quality-policy|generation|suite-review|publish)$/i.exec(
      url.pathname,
    );
  if (authoringAction) {
    if (!userName) return authenticationProblem(response);
    if (!state.users.get(userName)?.isAdmin) return forbiddenProblem(response);
    const action = authoringAction[2];
    if (request.method !== 'GET' && !hasValidCsrf(request, cookies)) return csrfProblem(response);
    if (request.method === 'PUT') {
      const body = await readJson(request);
      if (action === 'metadata') Object.assign(state.authoringDraft, body);
      if (action === 'signature')
        state.authoringDraft.definition.functionSignature = body.signature;
      if (action === 'handwritten-cases')
        state.authoringDraft.definition.handwrittenCases = body.cases;
      if (action === 'sources') Object.assign(state.authoringDraft.definition, body);
      if (action === 'quality-policy')
        state.authoringDraft.definition.qualityPolicy = body.qualityPolicy;
      return json(response, 200, state.authoringDraft);
    }
    if (action === 'generation' && request.method === 'POST') {
      state.generationPolls = 0;
      state.authoringDraft.status = 'Generating';
      return json(response, 202, generationStatus('Pending'));
    }
    if (action === 'generation' && request.method === 'GET') {
      state.generationPolls += 1;
      const done = state.generationPolls > 1;
      state.authoringDraft.status = done ? 'Ready' : 'Generating';
      return json(response, 200, generationStatus(done ? 'Succeeded' : 'Running'));
    }
    if (action === 'suite-review' && request.method === 'GET') {
      return json(response, 200, {
        revisionId: state.authoringDraft.revisionId,
        suiteSha256: 'a'.repeat(64),
        testCaseCount: 1000,
        casesByGroup: { handwritten: 1, edge: 100, random: 700, adversarial: 149, stress: 50 },
        wrongSolutionCount: 1,
        killedCaseCountByWrongSolution: { 'adjacent-only': 80 },
        survivingWrongSolutions: [],
        qualityPolicy: state.authoringDraft.definition.qualityPolicy,
        toolchain: 'e2e-generator-sdk-v1',
        casePreview: [
          {
            ordinal: 1,
            name: 'minimum',
            group: 'handwritten',
            seed: 0,
            killedWrongSolutions: ['adjacent-only'],
          },
        ],
        isCasePreviewTruncated: false,
      });
    }
    if (action === 'publish' && request.method === 'POST') {
      state.authoringDraft.status = 'Published';
      const managed = state.adminProblems.find(
        (item) => item.id === state.authoringDraft.problemId,
      );
      if (managed) {
        managed.status = 2;
        managed.latestRevisionStatus = 3;
        managed.publishedAt = '2026-07-27T00:00:00Z';
      }
      response.writeHead(204);
      return response.end();
    }
  }

  if (url.pathname === '/api/problems' && request.method === 'GET') {
    const search = (url.searchParams.get('Search') ?? '').trim().toLowerCase();
    const difficulty = url.searchParams.get('Difficulty');
    const solvedFilter = url.searchParams.get('Solved');
    const candidates = [
      problem,
      ...state.contentBatch.items
        .filter((item) => item.status === 3)
        .map((item) => ({
          id: item.problemId,
          slug: item.slug,
          title: item.title,
          difficulty: 'Easy',
          tags: [],
        })),
    ];
    const items = candidates
      .map((item) => ({
        ...item,
        isSolved: userName ? hasAcceptedSubmission(userName, item.id) : null,
      }))
      .filter(
        (item) =>
          (!search ||
            item.title.toLowerCase().includes(search) ||
            item.slug.toLowerCase().includes(search)) &&
          (!difficulty || difficulty === '1') &&
          (solvedFilter === null || solvedFilter === String(item.isSolved)),
      );
    return json(response, 200, page(items, url));
  }

  if (url.pathname === `/api/problems/${problem.slug}` && request.method === 'GET') {
    return json(response, 200, {
      ...problem,
      isSolved: userName ? hasAcceptedSubmission(userName) : null,
      statementMarkdown:
        'Given an array of integers, return the indices of two numbers that add up to a target.',
      constraintsMarkdown: '- Exactly one answer exists.\n- Use C++17.',
      timeLimitMs: 1000,
      memoryLimitKb: 262144,
      judgeVersion: 1,
      executionMode: 0,
      functionSignature: null,
      publishedAt: '2026-07-17T00:00:00Z',
      samples: [
        {
          ordinal: 1,
          input: '4\\n2 7 11 15\\n9',
          expectedOutput: '0 1',
          explanation: '2 + 7 equals 9.',
        },
      ],
    });
  }

  if (url.pathname === `/api/problems/${functionProblem.slug}` && request.method === 'GET') {
    return json(response, 200, {
      ...functionProblem,
      isSolved: userName ? hasAcceptedSubmission(userName, functionProblem.id) : null,
      statementMarkdown: 'Implement a method that doubles one integer.',
      constraintsMarkdown: '- The value fits in a signed 32-bit integer.\n- Use C++17.',
      timeLimitMs: 1000,
      memoryLimitKb: 262144,
      judgeVersion: 1,
      executionMode: 1,
      functionSignature: {
        className: 'Solution',
        methodName: 'solve',
        returnType: 0,
        parameters: [{ name: 'value', type: 0 }],
      },
      publishedAt: '2026-07-17T00:00:00Z',
      samples: [{ ordinal: 1, input: '{"value":2}', expectedOutput: '4', explanation: null }],
    });
  }

  if (
    state.authoringDraft &&
    url.pathname === `/api/problems/${state.authoringDraft.slug}` &&
    request.method === 'GET'
  ) {
    return json(response, 200, {
      id: state.authoringDraft.problemId,
      slug: state.authoringDraft.slug,
      title: state.authoringDraft.title,
      difficulty: state.authoringDraft.difficulty,
      tags: [],
      isSolved: userName ? hasAcceptedSubmission(userName, state.authoringDraft.problemId) : null,
      statementMarkdown: state.authoringDraft.statementMarkdown,
      constraintsMarkdown: state.authoringDraft.constraintsMarkdown,
      timeLimitMs: state.authoringDraft.timeLimitMs,
      memoryLimitKb: state.authoringDraft.memoryLimitKb,
      judgeVersion: 1,
      executionMode: 1,
      functionSignature: state.authoringDraft.definition.functionSignature,
      publishedAt: '2026-07-22T00:05:00Z',
      samples: state.authoringDraft.samples.map((sample, index) => ({
        ordinal: index + 1,
        ...sample,
      })),
    });
  }

  const createRunMatch = /^\/api\/problems\/([^/]+)\/runs$/i.exec(url.pathname);
  if (createRunMatch && request.method === 'POST') {
    if (!userName) return authenticationProblem(response);
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    state.runCreateRequests += 1;
    const body = await readJson(request);
    const isFunction = createRunMatch[1] === functionProblem.slug;
    const run = {
      id: randomUUID(),
      owner: userName,
      problemId: isFunction ? functionProblem.id : problem.id,
      status: 'Pending',
      stdout: null,
      stderr: null,
      executionTimeMs: null,
      memoryUsedKb: null,
      createdAt: '2026-07-17T00:30:00Z',
      startedAt: null,
      finishedAt: null,
      polls: 0,
      result: isFunction ? String(Number(body.arguments?.value) * 2) : String(body.input ?? ''),
    };
    state.runs.set(run.id, run);
    return json(response, 201, publicRun(run));
  }

  if (url.pathname === '/api/submissions' && request.method === 'POST') {
    if (!userName) return authenticationProblem(response);
    if (!hasValidCsrf(request, cookies)) return csrfProblem(response);
    state.createRequests += 1;
    const body = await readJson(request);
    await delay(250);
    const submission = {
      id: randomUUID(),
      owner: userName,
      problemId: Number(body.problemId),
      systemTestSuiteVersion: 1,
      language: body.language,
      status: 'Pending',
      executionTimeMs: null,
      memoryUsedKb: null,
      createdAt: '2026-07-17T01:00:00Z',
      startedAt: null,
      finishedAt: null,
      polls: 0,
      finalStatus: String(body.sourceCode).includes('WRONG') ? 'WrongAnswer' : 'Accepted',
    };
    state.submissions.set(submission.id, submission);
    return json(response, 201, publicSubmission(submission));
  }

  if (url.pathname === '/api/submissions' && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    const problemId = Number(url.searchParams.get('ProblemId') ?? 0);
    const status = Number(url.searchParams.get('Status') ?? 0);
    const statusByNumber = [
      '',
      'Pending',
      'Running',
      'Accepted',
      'WrongAnswer',
      'TimeLimitExceeded',
      'MemoryLimitExceeded',
      'CompileError',
      'RuntimeError',
    ];
    const submissions = [...state.submissions.values()]
      .filter((submission) => submission.owner === userName)
      .filter((submission) => !problemId || submission.problemId === problemId)
      .filter((submission) => !status || submission.status === statusByNumber[status])
      .map(publicSubmission);
    return json(response, 200, page(submissions, url));
  }

  const submissionMatch = /^\/api\/submissions\/([0-9a-f-]+)$/i.exec(url.pathname);
  if (submissionMatch && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    const submission = state.submissions.get(submissionMatch[1]);
    if (!submission) return problemDetails(response, 404, 'not-found', 'Submission not found.');
    if (submission.owner !== userName) {
      return problemDetails(response, 403, 'forbidden', 'Submission access denied.');
    }
    advanceSubmission(submission);
    return json(response, 200, publicSubmission(submission));
  }

  const runMatch = /^\/api\/runs\/([0-9a-f-]+)$/i.exec(url.pathname);
  if (runMatch && request.method === 'GET') {
    if (!userName) return authenticationProblem(response);
    const run = state.runs.get(runMatch[1]);
    if (!run) return problemDetails(response, 404, 'not-found', 'Run not found.');
    if (run.owner !== userName)
      return problemDetails(response, 403, 'forbidden', 'Run access denied.');
    advanceRun(run);
    return json(response, 200, publicRun(run));
  }

  return problemDetails(response, 404, 'not-found', 'API route not found.');
}

async function serveStatic(request, response, pathName) {
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    return problemDetails(response, 405, 'method-not-allowed', 'Method not allowed.');
  }
  const requestedPath = pathName === '/' ? '/index.html' : pathName;
  let filePath = resolve(root, `.${requestedPath}`);
  if (!filePath.startsWith(`${root}${sep}`) && filePath !== root) {
    return problemDetails(response, 404, 'not-found', 'Asset not found.');
  }
  try {
    if (!(await stat(filePath)).isFile()) throw new Error('Not a file.');
  } catch {
    filePath = resolve(root, 'index.html');
  }
  const content = await readFile(filePath);
  response.setHeader('Content-Type', contentType(filePath));
  response.setHeader('Cache-Control', 'no-store');
  response.writeHead(200);
  if (request.method === 'HEAD') return response.end();
  return response.end(content);
}

function advanceSubmission(submission) {
  submission.polls += 1;
  if (submission.status === 'Pending') {
    submission.status = 'Running';
    submission.startedAt = '2026-07-17T01:00:01Z';
  } else if (submission.status === 'Running') {
    submission.status = submission.finalStatus;
    submission.executionTimeMs = 12;
    submission.memoryUsedKb = 2048;
    submission.finishedAt = '2026-07-17T01:00:02Z';
  }
}

function advanceRun(run) {
  run.polls += 1;
  if (run.status === 'Pending') {
    run.status = 'Running';
    run.startedAt = '2026-07-17T00:30:01Z';
  } else if (run.status === 'Running') {
    run.status = 'Completed';
    run.stdout = run.result;
    run.stderr = '';
    run.executionTimeMs = 4;
    run.memoryUsedKb = 1024;
    run.finishedAt = '2026-07-17T00:30:02Z';
  }
}

function publicRun(run) {
  return {
    id: run.id,
    problemId: run.problemId,
    status: run.status,
    stdout: run.stdout,
    stderr: run.stderr,
    executionTimeMs: run.executionTimeMs,
    memoryUsedKb: run.memoryUsedKb,
    createdAt: run.createdAt,
    startedAt: run.startedAt,
    finishedAt: run.finishedAt,
  };
}

function publicSubmission(submission) {
  return {
    id: submission.id,
    problemId: submission.problemId,
    systemTestSuiteVersion: submission.systemTestSuiteVersion,
    language: submission.language,
    status: submission.status,
    executionTimeMs: submission.executionTimeMs,
    memoryUsedKb: submission.memoryUsedKb,
    createdAt: submission.createdAt,
    startedAt: submission.startedAt,
    finishedAt: submission.finishedAt,
  };
}

function hasAcceptedSubmission(userName, problemId = problem.id) {
  return [...state.submissions.values()].some(
    (submission) =>
      submission.owner === userName &&
      submission.problemId === problemId &&
      submission.status === 'Accepted',
  );
}

function hasValidCsrf(request, cookies) {
  return (
    cookies['XSRF-TOKEN'] === 'e2e-xsrf-token' &&
    request.headers['x-xsrf-token'] === 'e2e-xsrf-token'
  );
}

function setSessionCookie(response, sessionId) {
  response.setHeader(
    'Set-Cookie',
    `algojudge_session=${sessionId}; Path=/; HttpOnly; SameSite=Strict`,
  );
}

function authResponse(user) {
  return {
    userName: user.userName,
    email: user.email,
    isAdmin: user.isAdmin,
    expiresAt: '2026-07-17T02:00:00Z',
  };
}

function createUser(body) {
  const passwordSalt = randomBytes(16).toString('hex');
  return {
    userName: String(body.userName),
    email: String(body.email),
    fullName: String(body.fullName),
    isAdmin: String(body.userName).startsWith('admin_'),
    passwordSalt,
    passwordHash: scryptSync(String(body.password), passwordSalt, 32).toString('hex'),
  };
}

function managementDraft(problemItem) {
  return {
    revisionId: randomUUID(),
    problemId: problemItem.id,
    revisionNumber: 2,
    status: 'Draft',
    slug: problemItem.slug,
    title: problemItem.title,
    statementMarkdown: 'Update the problem statement for the next revision.',
    constraintsMarkdown: '- Use C++17.',
    difficulty: problemItem.difficulty,
    timeLimitMs: 1000,
    memoryLimitKb: 262144,
    samples: [{ input: '{"value":1}', expectedOutput: '2' }],
    definition: {
      schemaVersion: 1,
      executionMode: 'Function',
      functionSignature: {
        className: 'Solution',
        methodName: 'solve',
        returnType: 0,
        parameters: [],
      },
      handwrittenCases: [],
      generator: { language: 'csharp', sdkVersion: 1, source: '' },
      inputValidator: { language: 'csharp', sdkVersion: 1, source: '' },
      referenceSolution: { language: 'cpp17', source: '' },
      wrongSolutions: [],
      qualityPolicy: {
        minimumTestCaseCount: 1,
        minimumCasesByGroup: [{ group: 'handwritten', minimumCaseCount: 1 }],
        requireEachDeclaredWrongSolutionKilled: true,
      },
    },
    updatedAt: '2026-07-27T00:00:00Z',
  };
}

function passwordMatches(candidate, user) {
  const candidateHash = scryptSync(String(candidate), user.passwordSalt, 32);
  const expectedHash = Buffer.from(user.passwordHash, 'hex');
  return (
    candidateHash.length === expectedHash.length && timingSafeEqual(candidateHash, expectedHash)
  );
}

function page(items, url) {
  const pageNumber = Number(url.searchParams.get('PageNumber') ?? 1);
  const pageSize = Number(url.searchParams.get('PageSize') ?? 20);
  return {
    items,
    pageNumber,
    pageSize,
    totalCount: items.length,
    totalPages: items.length === 0 ? 0 : 1,
  };
}

function createScaleContentBatch() {
  const id = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  const createdAt = '2026-07-28T08:00:00Z';
  const items = Array.from({ length: 100 }, (_, index) => {
    const ordinal = index + 1;
    const suffix = String(ordinal).padStart(3, '0');
    const failed = ordinal >= 91 && ordinal <= 95;
    const skipped = ordinal >= 96 && ordinal <= 98;
    const invalid = ordinal >= 99;
    const category =
      ordinal === 91
        ? 'compile_error'
        : ordinal === 92
          ? 'quality_gate_failed'
          : ordinal === 93
            ? 'worker_unavailable'
            : ordinal === 94
              ? 'reference_failed'
              : ordinal === 95
                ? 'validator_failed'
                : ordinal === 99
                  ? 'duplicate_slug'
                  : ordinal === 100
                    ? 'invalid_path'
                    : null;
    const names = {
      1: ['template-only', 'Template Only'],
      2: ['override-generator', 'Override Generator'],
      3: ['override-validator', 'Override Validator'],
      4: ['wrong-solutions', 'Wrong Solutions'],
      91: ['compile-fail', 'Intentional Compile Failure'],
      92: ['quality-gate-fail', 'Intentional Quality Gate Failure'],
      99: ['duplicate-slug', 'Invalid Duplicate Slug'],
      100: ['invalid-path', 'Invalid Path'],
    };
    const [slug, title] = names[ordinal] ?? [`problem-${suffix}`, `Problem ${suffix}`];
    return {
      id: `10000000-0000-0000-0000-${suffix.padStart(12, '0')}`,
      ordinal,
      catalogPath: `problems/${slug}`,
      slug,
      title,
      action: ordinal === 90 ? 2 : 0,
      status: failed || invalid ? 4 : skipped ? 6 : 2,
      contentHash: suffix.padStart(64, '0'),
      problemId: invalid ? null : 1000 + ordinal,
      revisionId: skipped || invalid ? null : `20000000-0000-0000-0000-${suffix.padStart(12, '0')}`,
      safeFailureCategory: category,
      safeFailureMessage: category ? safeBatchMessage(category) : null,
      updatedAt: createdAt,
    };
  });
  const batch = {
    id,
    catalogName: 'acceptance-100/catalog.json',
    status: 3,
    createdByUserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    counts: {},
    items,
    auditEntries: [batchAudit('batch.start', 'completed')],
    createdAt,
    updatedAt: createdAt,
    startedAt: createdAt,
    completedAt: null,
  };
  refreshBatchCounts(batch);
  return batch;
}

function refreshBatchCounts(batch) {
  batch.counts = {
    total: batch.items.length,
    pending: batch.items.filter((item) => item.status === 0).length,
    generating: batch.items.filter((item) => item.status === 1 || item.status === 5).length,
    ready: batch.items.filter((item) => item.status === 2).length,
    failed: batch.items.filter((item) => item.status === 4).length,
    published: batch.items.filter((item) => item.status === 3).length,
    skipped: batch.items.filter((item) => item.status === 6).length,
  };
  batch.updatedAt = new Date().toISOString();
}

function batchAudit(action, result) {
  return {
    id: Date.now(),
    adminUserId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    itemId: null,
    problemId: null,
    revisionId: null,
    action,
    result,
    safeFailureCategory: null,
    createdAt: new Date().toISOString(),
  };
}

function safeBatchMessage(category) {
  const messages = {
    compile_error: 'Generator compilation failed.',
    quality_gate_failed: 'The generated suite did not satisfy its quality policy.',
    worker_unavailable: 'The generation attempt was interrupted.',
    reference_failed: 'Reference execution failed.',
    validator_failed: 'Generated input validation failed.',
    duplicate_slug: 'The catalog contains a duplicate problem slug.',
    invalid_path: 'The workspace item contains an unsafe or invalid path.',
  };
  return messages[category] ?? 'The item failed.';
}

function setSecurityHeaders(response) {
  for (const [name, value] of Object.entries(securityHeaders)) response.setHeader(name, value);
}

function parseCookies(header = '') {
  return Object.fromEntries(
    header
      .split(';')
      .map((value) => value.trim().split('='))
      .filter(([name, value]) => name && value)
      .map(([name, ...value]) => [name, decodeURIComponent(value.join('='))]),
  );
}

async function readJson(request) {
  const chunks = [];
  for await (const chunk of request) chunks.push(chunk);
  return JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
}

function json(response, status, body) {
  response.setHeader('Content-Type', 'application/json; charset=utf-8');
  response.writeHead(status);
  return response.end(JSON.stringify(body));
}

function problemDetails(response, status, code, title, detail = null) {
  return json(response, status, {
    status,
    code,
    title,
    detail,
    type: 'about:blank',
  });
}

function authenticationProblem(response) {
  return problemDetails(response, 401, 'authentication', 'Authentication required.');
}

function forbiddenProblem(response) {
  return problemDetails(response, 403, 'forbidden', 'Administrator access required.');
}

function csrfProblem(response) {
  return problemDetails(response, 403, 'csrf', 'Antiforgery validation failed.');
}

function contentType(filePath) {
  return (
    {
      '.css': 'text/css; charset=utf-8',
      '.html': 'text/html; charset=utf-8',
      '.ico': 'image/x-icon',
      '.js': 'text/javascript; charset=utf-8',
      '.json': 'application/json; charset=utf-8',
    }[extname(filePath).toLowerCase()] ?? 'application/octet-stream'
  );
}

function delay(milliseconds) {
  return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

function generationStatus(jobStatus) {
  return {
    jobId: '11111111-1111-1111-1111-111111111111',
    revisionId: state.authoringDraft.revisionId,
    jobStatus,
    revisionStatus: state.authoringDraft.status,
    attemptCount: 1,
    errorCode: null,
    errorMessage: null,
    createdAt: '2026-07-22T00:01:00Z',
    startedAt: '2026-07-22T00:01:01Z',
    finishedAt: jobStatus === 'Succeeded' ? '2026-07-22T00:01:02Z' : null,
  };
}
