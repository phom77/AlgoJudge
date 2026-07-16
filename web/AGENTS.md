# AlgoJudge Web Development Rules

These instructions apply to every file under `web/`. They supplement the root
`AGENTS.md` and are the default frontend review checklist.

## Product and framework boundary

- Build only authentication, problem catalogue/workspace, the C++17 editor,
  submission polling/detail/history, and solved state for the learner MVP.
- Use standalone Angular APIs, strict TypeScript/templates, SCSS, typed reactive
  forms, Signals, and RxJS. Do not add NgRx without an accepted decision backed
  by measured state complexity.
- Pin framework and generator versions. Generate the typed client from
  `/openapi/v1.json`; never hand-copy backend contracts or edit generated files.

## Required structure and dependencies

```text
src/app/
  core/
    api/generated/
    auth/
    config/
    error/
    layout/
  shared/
    ui/
    directives/
    pipes/
    utils/
  features/
    auth/
    problems/
      data-access/
      catalogue/
      workspace/
      ui/
    submissions/
      data-access/
      history/
      detail/
      ui/
```

- `core` owns singleton infrastructure and never imports a feature.
- `shared` owns business-neutral UI and never imports `core`, a feature, or the
  generated API.
- Feature pages may use their own UI/data-access and shared UI.
- Presentational UI never calls HTTP, reads cookies, or owns route-level state.
- Generated API code is imported only by `core/api` and feature `data-access`.
- Avoid global type folders named only `components`, `services`, or `models`.
- Avoid barrel `index.ts` files until a real public boundary requires one.

## File roles

- `*.page.ts`: routed container and UI orchestration only.
- `*.component.ts`: focused presentational interaction.
- `*.store.ts`: feature state, actions, loading/error, and computed state.
- `*.gateway.ts`: wrapper around the generated API client.
- `*.polling.service.ts`: polling lifecycle, cancellation, and terminal status.
- `*.mapper.ts`: pure API-to-view-model transformations.
- `*.form.ts`: typed form factories and validators.
- `*.routes.ts`, `*.guard.ts`, and `*.interceptor.ts`: framework boundaries.
- Keep component TypeScript, HTML, SCSS, and Vitest spec files together. Keep
  one primary concept per file and use domain-specific names.

## God-component prevention

- Pages connect route parameters and stores to children. They do not implement
  raw HTTP, polling, token handling, Markdown sanitization, Monaco integration,
  DTO mapping, and complex forms in one class.
- Extract a component when a visual section has its own state, events,
  lifecycle, loading/error branch, reuse value, or independent test value.
- Five or more injected dependencies requires decomposition review. Treat
  roughly 200 TypeScript logic lines or 150 template lines as a review
  threshold, not a target.
- Move pure validation and transformations out of components. Keep template
  expressions simple and derive non-trivial UI with `computed()`.
- Never nest subscriptions. Use `switchMap`, `takeUntilDestroyed`, the async
  pipe, or a deliberate Observable-to-Signal boundary.
- Compose the workspace from statement, samples, editor, toolbar, result panel,
  and verdict components. Wrap Monaco behind `CodeEditorComponent` and lazy-load
  it only for the workspace.

## State management

- Use Signals for synchronous UI state and `computed()` for derived state.
- Use RxJS for HTTP, debounce, polling, cancellation, and multi-event flows.
- Use `effect()` only for an imperative external API; never use it to copy state
  or trigger ordinary data fetching.
- Maintain one source of truth, do not persist computable state, and do not
  mutate arrays or objects in place.
- Catalogue filters and pagination live in router query parameters.
- Submission polling stops on every final verdict, route change, logout, and
  component destruction. Only one poll may exist per submission.

## Authentication, cookies, and sensitive data

- Access and refresh credentials are backend-managed cookies with `HttpOnly`,
  `Secure`, and `SameSite=Strict`. Angular must never read them.
- Never store credentials in localStorage, sessionStorage, IndexedDB, Signals,
  NgRx, service fields, URLs, analytics, error reports, or logs.
- The readable `XSRF-TOKEN` cookie is an antiforgery token, not an authentication
  credential. Angular sends it only as `X-XSRF-TOKEN` on unsafe same-origin API
  requests.
- Bootstrap antiforgery with `GET /api/auth/csrf` before register, login,
  refresh, revoke, or submission creation.
- Restore login state with `GET /api/auth/session`; never infer authentication
  from the existence of a JavaScript value.
- Refresh is single-flight. Concurrent `401` responses cause at most one
  refresh operation. Never automatically replay `POST /api/submissions`.
- Logout calls `POST /api/auth/revoke` and clears in-memory user state even when
  the network operation fails.
- Development uses the Angular proxy and production serves SPA/API from one
  origin. Do not solve cookie issues with broad credentialed CORS.
- Route guards are UX controls only; backend authorization is authoritative.
- Do not persist source code by default. A future opt-in draft feature requires
  a retention/privacy design; cookies are never used for source, PII, or
  arbitrary application data.

## HTTP and contract rules

- Components never import `HttpClient` or generated API services directly.
- Gateways expose domain operations and map Problem Details to one frontend
  `ApiProblem` model.
- Handle `400`, `401`, `403`, `404`, `409`, `429`, and `csrf` explicitly.
- Respect `Retry-After`. Do not automatically retry validation, authentication,
  ownership, CSRF, or submission creation failures.
- Never log bodies, credentials, passwords, source code, or hidden-test data.
- CI regenerates the typed client and fails on uncommitted contract drift.

## Routing, forms, visual design, and accessibility

- Use `/login`, `/register`, `/problems`, `/problems/:slug`, `/submissions`, and
  `/submissions/:id`. Lazy-load auth, workspace, submission pages, and Monaco.
- Validate redirect targets as internal paths. Guards do not fetch unrelated data.
- Use strictly typed reactive forms. Mirror backend validation for UX while
  treating backend errors as authoritative.
- Use a LeetCode-like information architecture without copying branding:
  compact navigation, problem table, neutral surfaces, difficulty badges, and
  a desktop split workspace. Mobile uses accessible tabs/panels.
- Define design tokens as CSS custom properties. Global CSS contains only reset,
  tokens, typography, and intentional utilities; component styles stay local.
- Do not use `::ng-deep`, arbitrary `!important`, clickable `div` elements, or
  direct DOM manipulation. Support keyboard, visible focus, labels, and ARIA.
- Verdict and difficulty always include text or an accessible icon, not color alone.
- Treat backend Markdown as untrusted. `bypassSecurityTrustHtml` requires a
  dedicated security review and tests.

## Testing and quality gates

- Keep Vitest specs beside the code under test. Test stores, mappers, forms,
  polling, refresh coordination, gateways, interceptors, and Problem Details.
- Component tests cover rendered user behavior, loading, empty, error, disabled,
  keyboard, and narrow-screen states.
- Playwright covers auth, catalogue filters, workspace submission, final verdict,
  history, solved state, session restore, CSRF rejection, and logout.
- CI runs formatting, lint, strict type checking, unit tests, production build,
  bundle budgets, generated-client drift, and critical Playwright flows.
- A feature is incomplete without success, validation, authorization, network
  failure, loading, and empty states.
