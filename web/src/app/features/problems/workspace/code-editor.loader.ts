import { DOCUMENT } from '@angular/common';
import { inject, InjectionToken, RendererFactory2 } from '@angular/core';
import type { Renderer2 } from '@angular/core';
import type * as Monaco from 'monaco-editor';

export type MonacoModule = typeof Monaco;
export type MonacoLoader = () => Promise<MonacoModule>;

interface AmdRequire {
  (modules: readonly string[], onLoad: () => void, onError: (error: unknown) => void): void;
  config(options: { paths: { vs: string } }): void;
}

interface MonacoWindow extends Window {
  readonly monaco?: MonacoModule;
  readonly require?: AmdRequire;
}

let monacoPromise: Promise<MonacoModule> | null = null;

export const MONACO_LOADER = new InjectionToken<MonacoLoader>('MONACO_LOADER', {
  providedIn: 'root',
  factory: () => {
    const document = inject(DOCUMENT);
    const renderer = inject(RendererFactory2).createRenderer(null, null);
    return () => loadMonaco(document, renderer);
  },
});

function loadMonaco(document: Document, renderer: Renderer2): Promise<MonacoModule> {
  monacoPromise ??= new Promise<MonacoModule>((resolve, reject) => {
    const baseUrl = new URL('assets/monaco/vs', document.baseURI).href.replace(/\/$/, '');
    const script = renderer.createElement('script') as HTMLScriptElement;
    renderer.setAttribute(script, 'src', `${baseUrl}/loader.js`);
    renderer.setAttribute(script, 'data-aj-monaco-loader', '');
    renderer.listen(script, 'load', () =>
      loadEditor(document.defaultView, baseUrl, resolve, reject),
    );
    renderer.listen(script, 'error', () =>
      reject(new Error('The Monaco loader could not be loaded.')),
    );
    renderer.appendChild(document.body, script);
  });
  return monacoPromise;
}

function loadEditor(
  view: (Window & typeof globalThis) | null,
  baseUrl: string,
  resolve: (module: MonacoModule) => void,
  reject: (error: unknown) => void,
): void {
  const target = view as MonacoWindow | null;
  if (target?.require === undefined) {
    reject(new Error('The Monaco AMD loader is unavailable.'));
    return;
  }
  target.require.config({ paths: { vs: baseUrl } });
  target.require(
    ['vs/editor/editor.main'],
    () =>
      target.monaco === undefined
        ? reject(new Error('Monaco did not initialize.'))
        : resolve(target.monaco),
    reject,
  );
}
