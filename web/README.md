# AlgoJudge Web

This directory is reserved for the browser application.

The frontend framework has not been selected yet. Create an ADR before adding
the package manager lockfile or framework scaffold. Regardless of framework,
the application should keep these boundaries:

- `src/app` — application bootstrap and routing
- `src/features` — problem catalogue, editor, submissions, auth, and profile
- `src/shared` — reusable UI, API client, configuration, and utilities
- `public` — static public assets
- `tests` — frontend integration and end-to-end tests
