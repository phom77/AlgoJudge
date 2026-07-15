# AlgoJudge Web

This directory is reserved for the Angular browser application. The framework
has been selected, but the workspace will be scaffolded only after the backend
API and judge contract are stable.

The Angular application should keep these boundaries:

- `src/app` — application bootstrap and routing
- `src/features` — problem catalogue, editor, submissions, auth, and profile
- `src/shared` — reusable UI, API client, configuration, and utilities
- `public` — static public assets
- `tests` — frontend integration and end-to-end tests
