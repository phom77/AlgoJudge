# AlgoJudge Web

Angular browser client for AlgoJudge. The workspace uses Angular 21 standalone
APIs, strict TypeScript and template checking, zoneless change detection, SCSS,
Vitest, angular-eslint, and Prettier.

The frontend implementation rules live in [AGENTS.md](AGENTS.md). They are the
required review checklist for every change under `web/`.

## Prerequisites

- Node.js 20.20.2 (pinned in `.nvmrc`)
- npm 10.8.2
- AlgoJudge API listening on `http://localhost:5016`

## Local development

```bash
npm ci
npm start
```

Open `http://localhost:4200`. The Angular development server proxies `/api` to
the backend through `proxy.conf.json`. Keep browser API calls relative (for
example, `/api/problems`) so secure session and antiforgery cookies remain
same-origin.

Before an unsafe authenticated request, the auth feature must call
`GET /api/auth/csrf`. Angular's HTTP provider is configured to mirror the
readable `XSRF-TOKEN` cookie in the `X-XSRF-TOKEN` header. Access and refresh
cookies remain HttpOnly and must never be read or persisted by frontend code.

## Commands

```bash
npm start             # development server with API proxy
npm run api:generate  # regenerate the typed client from the approved v1 snapshot
npm run api:check     # fail when checked-in generated code has contract drift
npm run format        # apply Prettier
npm run format:check  # verify formatting
npm run lint          # TypeScript and Angular template lint
npm run test          # one Vitest run
npm run test:watch    # interactive Vitest watch mode
npm run test:coverage # Vitest coverage
npm run build         # optimized production build with budgets
npm run security:check # verify production CSP, Trusted Types, and artifacts
npm run test:e2e      # build and run Chromium MVP acceptance + accessibility
npm run check         # static, unit, production build, and security gates
```

Playwright uses the optimized bundle and a deterministic same-origin acceptance
server under `e2e/support`. The server models the stable cookie, CSRF, problem,
submission, polling, history, and solved-status contracts; it is test-only and
does not replace backend end-to-end acceptance against PostgreSQL and Docker.
Install the local browser once with `npx playwright install chromium`.

Frontend CI runs the critical registration/login, catalogue, C++17 submission,
final verdict, session restore, history, logout, CSP/Trusted Types, desktop
accessibility, and mobile workspace checks. Failure traces, screenshots, and
videos are uploaded for seven days.

## Production security and budgets

The optimized index contains only external same-origin scripts and no inline
event handlers. Deployment
infrastructure must emit the CSP and other headers declared in
`config/security-headers.json`; the acceptance server applies that exact file so
browser tests exercise the deployable policy, including Trusted Types. Keep the
Angular and Monaco policy names synchronized with `code-editor.loader.ts`.

The production build fails when the initial bundle exceeds 400 kB, an individual
script exceeds 3 MB, or a component stylesheet exceeds 8 kB. Warning thresholds
are intentionally lower. Monaco's layout stylesheet is generated before build,
loaded only with the workspace, and independently capped at 100 kB.
`security:check` also fails for inline executable scripts, missing strict CSP,
unsafe eval, missing baseline headers, missing Monaco layout CSS, or production
source maps.

## Source layout

```text
src/app/
  core/                  singleton infrastructure, auth, errors, and layout
    api/generated/       generated OpenAPI client; never edit by hand
  shared/                business-neutral UI, directives, pipes, and utilities
  features/
    auth/
    problems/
      data-access/
      catalogue/
      workspace/
      ui/
    submissions/
      data-access/
      detail/
      history/
      ui/
src/styles/              reset, typography, and CSS custom-property tokens
```

Routes are lazy-loaded at feature boundaries. Routed `*.page.ts` files
orchestrate state and focused child components; HTTP and DTO mapping belong in
feature data-access gateways. Tests stay beside the code they cover.

## OpenAPI client

`ng-openapi-gen.json` is the single generator configuration. It reads the
approved backend snapshot at
`tests/AlgoJudge.Api.IntegrationTests/Snapshots/openapi-v1.json` and writes the
typed Angular client to `src/app/core/api/generated`. Generated files are
committed for review but must never be edited by hand.

Run `npm run api:generate` after an intentional, reviewed OpenAPI snapshot
change. `npm run api:check` generates into a temporary directory and compares
every file with the checked-in client, including added and stale files. Frontend
CI runs this drift check without modifying the workspace.

Application code configures the generated client through `provideAlgoJudgeApi`
and keeps its root URL same-origin. Feature gateways may import generated
operations and models; components must not. Convert `HttpErrorResponse` values
with `mapProblemDetails` so validation, CSRF, authorization, rate-limit, network,
and unknown failures share one safe `ApiProblem` shape.

## Browser authentication

Authentication credentials are backend-managed HttpOnly cookies and never
enter Angular state or browser storage. The app initializer restores public user
metadata with `GET /api/auth/session`; an expired access session may perform one
single-flight refresh using the restricted refresh cookie.

`AuthApiGateway` bootstraps antiforgery state before register, login, refresh,
and revoke. Angular mirrors `XSRF-TOKEN` into `X-XSRF-TOKEN` through its built-in
same-origin XSRF support. The refresh interceptor retries only safe API reads and
never automatically replays `POST /api/submissions`. Logout clears the in-memory
auth store even when revoke cannot reach the server.

## Toolchain policy

Framework and tool versions are exact in `package.json` and reproducible through
`package-lock.json`. Use `npm ci` in CI. Upgrade Angular deliberately as a
separate change after confirming the workspace Node runtime satisfies the new
Angular CLI engine requirement.
