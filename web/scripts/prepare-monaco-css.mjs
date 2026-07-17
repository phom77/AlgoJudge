import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

import { build } from 'esbuild';

const workspaceRoot = resolve(import.meta.dirname, '..');
const outputDirectory = resolve(workspaceRoot, '.generated');
const outputPath = resolve(outputDirectory, 'monaco-editor.css');

const result = await build({
  absWorkingDir: workspaceRoot,
  bundle: true,
  entryPoints: ['node_modules/monaco-editor/esm/vs/editor/editor.api.js'],
  format: 'esm',
  logLevel: 'warning',
  minify: true,
  outdir: 'monaco-css-build',
  platform: 'browser',
  treeShaking: true,
  write: false,
});

const stylesheet = result.outputFiles.find((file) => file.path.endsWith('.css'));

if (!stylesheet) {
  throw new Error('Monaco stylesheet generation produced no CSS output.');
}

await mkdir(outputDirectory, { recursive: true });
await writeFile(outputPath, stylesheet.contents);

console.log(`Generated ${outputPath} (${stylesheet.contents.byteLength} bytes).`);
