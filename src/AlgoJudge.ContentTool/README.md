# AlgoJudge ContentTool

This internal command-line application will validate and import versioned
problem packages. It is intentionally separate from the public API.

Planned commands:

- `validate <package-path>`
- `import <package-path>`
- `publish <problem-slug>`
- `unpublish <problem-slug>`

The package format must be approved through an ADR before these commands are
implemented.
