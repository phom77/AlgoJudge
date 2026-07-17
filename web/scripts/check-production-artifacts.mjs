import { readdir, readFile, stat } from 'node:fs/promises';
import { resolve } from 'node:path';

const outputRoot = resolve('dist/algojudge-web/browser');
const indexHtml = await readFile(resolve(outputRoot, 'index.html'), 'utf8');
const securityHeaders = JSON.parse(await readFile(resolve('config/security-headers.json'), 'utf8'));
const failures = [];

if (/<script(?![^>]+src=)[^>]*>/i.test(indexHtml)) {
  failures.push('The optimized index contains an inline executable script.');
}
if (/\son[a-z]+\s*=/i.test(indexHtml)) {
  failures.push('The optimized index contains an inline event handler.');
}

const productionCsp = securityHeaders['Content-Security-Policy'] ?? '';
for (const directive of [
  "frame-ancestors 'none'",
  "object-src 'none'",
  "script-src 'self'",
  'trusted-types angular angular#bundler algojudge-monaco',
  "require-trusted-types-for 'script'",
]) {
  if (!productionCsp.includes(directive)) {
    failures.push(`Production CSP is missing: ${directive}`);
  }
}
if (productionCsp.includes("'unsafe-eval'")) failures.push('Production CSP permits unsafe-eval.');
if (/script-src[^;]*'unsafe-inline'/.test(productionCsp)) {
  failures.push('Production script-src permits unsafe-inline.');
}
for (const header of [
  'Content-Security-Policy',
  'Permissions-Policy',
  'Referrer-Policy',
  'X-Content-Type-Options',
  'X-Frame-Options',
]) {
  if (!securityHeaders[header]) failures.push(`Production security header is missing: ${header}`);
}

const outputFiles = await listFiles(outputRoot);
const sourceMaps = outputFiles.filter((file) => file.endsWith('.map'));
if (sourceMaps.length > 0) {
  failures.push(`Production output contains source maps: ${sourceMaps.join(', ')}`);
}

const monacoStylesheetPath = resolve(outputRoot, 'assets/monaco/0.53.0/monaco-editor.css');
try {
  const [monacoStylesheet, monacoStylesheetStats] = await Promise.all([
    readFile(monacoStylesheetPath, 'utf8'),
    stat(monacoStylesheetPath),
  ]);
  if (!monacoStylesheet.includes('.monaco-editor .ime-text-area')) {
    failures.push('The generated Monaco stylesheet is missing editor layout rules.');
  }
  if (monacoStylesheetStats.size > 100 * 1024) {
    failures.push(
      `The generated Monaco stylesheet is ${monacoStylesheetStats.size} bytes (limit: 102400).`,
    );
  }
} catch {
  failures.push('The lazy Monaco stylesheet is missing from production output.');
}

if (failures.length > 0) {
  process.stderr.write(`${failures.map((failure) => `- ${failure}`).join('\n')}\n`);
  process.exitCode = 1;
} else {
  process.stdout.write('Production CSP, Trusted Types headers, and artifact checks passed.\n');
}

async function listFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const path = resolve(directory, entry.name);
    if (entry.isDirectory()) files.push(...(await listFiles(path)));
    else files.push(path);
  }
  return files;
}
