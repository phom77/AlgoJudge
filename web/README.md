# AlgoJudge Web

This directory is reserved for the Angular browser application. The framework
has been selected, but the workspace will be scaffolded only after the backend
API and judge contract are stable.

The Angular application keeps these boundaries:

- `src/app`: application bootstrap and routing.
- `src/app/features`: auth, problem catalogue/workspace, and submissions.
- `src/app/core`: generated API client, cookie-session state, configuration,
  errors, and application layout.
- `src/app/shared`: business-neutral reusable UI, directives, and pipes.
- `public`: static public assets.
- `e2e`: frontend end-to-end tests.

See `AGENTS.md` in this directory for the mandatory frontend architecture,
security, styling, testing, and god-component prevention rules.
