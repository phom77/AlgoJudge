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

  if (url.pathname === '/api/problems' && request.method === 'GET') {
    const search = (url.searchParams.get('Search') ?? '').trim().toLowerCase();
    const difficulty = url.searchParams.get('Difficulty');
    const solvedFilter = url.searchParams.get('Solved');
    const solved = userName ? hasAcceptedSubmission(userName) : null;
    const matches =
      (!search || problem.title.toLowerCase().includes(search)) &&
      (!difficulty || difficulty === '1') &&
      (solvedFilter === null || solvedFilter === String(solved));
    const items = matches ? [{ ...problem, isSolved: solved }] : [];
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
    submission.status = 'Accepted';
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
    expiresAt: '2026-07-17T02:00:00Z',
  };
}

function createUser(body) {
  const passwordSalt = randomBytes(16).toString('hex');
  return {
    userName: String(body.userName),
    email: String(body.email),
    fullName: String(body.fullName),
    passwordSalt,
    passwordHash: scryptSync(String(body.password), passwordSalt, 32).toString('hex'),
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
