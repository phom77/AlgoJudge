import { DOCUMENT } from '@angular/common';
import { inject, InjectionToken } from '@angular/core';
import type * as Monaco from 'monaco-editor';

export type MonacoModule = typeof Monaco;
export type MonacoLoader = () => Promise<MonacoModule>;

let monacoPromise: Promise<MonacoModule> | null = null;
let monacoStylesPromise: Promise<void> | null = null;
let monacoTrustedTypesPolicy: Monaco.ITrustedTypePolicy | undefined;

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
    loadMonacoStyles(document),
    import('monaco-editor/esm/vs/editor/editor.api'),
    import('monaco-editor/esm/vs/basic-languages/cpp/cpp.contribution'),
  ]).then(([, monaco]) => monaco as MonacoModule);
  return monacoPromise;
}

function loadMonacoStyles(document: Document): Promise<void> {
  monacoStylesPromise ??= new Promise<void>((resolve, reject) => {
    const existingStylesheet = document.querySelector<HTMLLinkElement>(
      'link[data-algojudge-monaco-styles]',
    );

    if (existingStylesheet?.sheet) {
      resolve();
      return;
    }

    const stylesheet = existingStylesheet ?? document.createElement('link');
    stylesheet.rel = 'stylesheet';
    stylesheet.href = new URL('assets/monaco/0.53.0/monaco-editor.css', document.baseURI).href;
    stylesheet.dataset['algojudgeMonacoStyles'] = '';
    stylesheet.addEventListener('load', () => resolve(), { once: true });
    stylesheet.addEventListener(
      'error',
      () => reject(new Error('Unable to load the Monaco editor stylesheet.')),
      { once: true },
    );

    if (!existingStylesheet) {
      document.head.append(stylesheet);
    }
  });

  return monacoStylesPromise;
}

function configureWorker(document: Document): void {
  const workerUrl = new URL(
    'assets/monaco/0.53.0/esm/vs/editor/editor.worker.js',
    document.baseURI,
  );
  const target = globalThis as typeof globalThis & {
    MonacoEnvironment?: Monaco.Environment;
    trustedTypes?: {
      createPolicy(
        name: string,
        options: Required<Monaco.ITrustedTypePolicyOptions>,
      ): Monaco.ITrustedTypePolicy;
    };
  };
  monacoTrustedTypesPolicy ??= target.trustedTypes?.createPolicy('algojudge-monaco', {
    createHTML: (value) => value,
    createScript: (value) => value,
    createScriptURL: (value) => value,
  });
  target.MonacoEnvironment ??= {
    createTrustedTypesPolicy: () => monacoTrustedTypesPolicy,
    getWorker: () =>
      new Worker(monacoTrustedTypesPolicy?.createScriptURL?.(workerUrl.href) ?? workerUrl, {
        type: 'module',
      }),
  };
}
