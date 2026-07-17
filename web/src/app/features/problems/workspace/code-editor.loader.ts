import { DOCUMENT } from '@angular/common';
import { inject, InjectionToken } from '@angular/core';
import type * as Monaco from 'monaco-editor';

export type MonacoModule = typeof Monaco;
export type MonacoLoader = () => Promise<MonacoModule>;

let monacoPromise: Promise<MonacoModule> | null = null;

export const MONACO_LOADER = new InjectionToken<MonacoLoader>('MONACO_LOADER', {
  providedIn: 'root',
  factory: () => {
    const document = inject(DOCUMENT);
    return () => loadMonaco(document);
  },
});

function loadMonaco(document: Document): Promise<MonacoModule> {
  configureWorker(document);
  monacoPromise ??= Promise.all([
    import('monaco-editor/esm/vs/editor/editor.api'),
    import('monaco-editor/esm/vs/basic-languages/cpp/cpp.contribution'),
  ]).then(([monaco]) => monaco as MonacoModule);
  return monacoPromise;
}

function configureWorker(document: Document): void {
  const workerUrl = new URL(
    'assets/monaco/0.53.0/esm/vs/editor/editor.worker.js',
    document.baseURI,
  );
  const target = globalThis as typeof globalThis & {
    MonacoEnvironment?: { getWorker: () => Worker };
  };
  target.MonacoEnvironment ??= {
    getWorker: () => new Worker(workerUrl, { type: 'module' }),
  };
}
