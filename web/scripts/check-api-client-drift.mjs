import { spawnSync } from 'node:child_process';
import { mkdtempSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, relative, resolve } from 'node:path';

const workspaceRoot = resolve(import.meta.dirname, '..');
const checkedInDirectory = resolve(workspaceRoot, 'src/app/core/api/generated');
const temporaryRoot = mkdtempSync(join(tmpdir(), 'algojudge-api-client-'));
const generatedDirectory = join(temporaryRoot, 'generated');

try {
  generateClient(generatedDirectory);

  const differences = compareDirectories(checkedInDirectory, generatedDirectory);
  if (differences.length > 0) {
    console.error('The generated OpenAPI client is out of date:');
    for (const difference of differences) {
      console.error(`- ${difference}`);
    }
    console.error('Run "npm run api:generate" and commit the generated changes.');
    process.exitCode = 1;
  } else {
    console.log('The generated OpenAPI client matches the approved v1 snapshot.');
  }
} finally {
  rmSync(temporaryRoot, { recursive: true, force: true });
}

function generateClient(outputDirectory) {
  const generator = resolve(workspaceRoot, 'node_modules/ng-openapi-gen/lib/index.js');
  const result = spawnSync(
    process.execPath,
    [generator, '--config', 'ng-openapi-gen.json', '--output', outputDirectory, '--silent', 'true'],
    {
      cwd: workspaceRoot,
      encoding: 'utf8',
      shell: false,
    },
  );

  if (result.status !== 0) {
    if (result.error) {
      console.error(result.error.message);
    }
    if (result.stdout) {
      process.stdout.write(result.stdout);
    }
    if (result.stderr) {
      process.stderr.write(result.stderr);
    }
    throw new Error('OpenAPI client generation failed.');
  }
}

function compareDirectories(checkedInRoot, generatedRoot) {
  const checkedInFiles = listFiles(checkedInRoot);
  const generatedFiles = listFiles(generatedRoot);
  const allFiles = [...new Set([...checkedInFiles, ...generatedFiles])].sort();
  const differences = [];

  for (const file of allFiles) {
    const checkedInPath = join(checkedInRoot, file);
    const generatedPath = join(generatedRoot, file);

    if (!checkedInFiles.includes(file)) {
      differences.push(`missing generated file: ${file}`);
      continue;
    }
    if (!generatedFiles.includes(file)) {
      differences.push(`stale generated file: ${file}`);
      continue;
    }
    if (readNormalizedText(checkedInPath) !== readNormalizedText(generatedPath)) {
      differences.push(`content differs: ${file}`);
    }
  }

  return differences;
}

function readNormalizedText(path) {
  return readFileSync(path, 'utf8').replace(/\r\n?/g, '\n');
}

function listFiles(root) {
  try {
    return walk(root).sort();
  } catch (error) {
    if (error && typeof error === 'object' && 'code' in error && error.code === 'ENOENT') {
      return [];
    }
    throw error;
  }
}

function walk(root, current = root) {
  const files = [];

  for (const entry of readdirSync(current, { withFileTypes: true })) {
    const path = join(current, entry.name);
    if (entry.isDirectory()) {
      files.push(...walk(root, path));
    } else if (entry.isFile() && statSync(path).isFile()) {
      files.push(relative(root, path));
    }
  }

  return files;
}
