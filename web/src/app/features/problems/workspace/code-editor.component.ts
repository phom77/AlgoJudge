import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import type { ElementRef, OnChanges } from '@angular/core';
import type { editor } from 'monaco-editor';

import { MONACO_LOADER } from './code-editor.loader';

@Component({
  selector: 'aj-code-editor',
  templateUrl: './code-editor.component.html',
  styleUrl: './code-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CodeEditorComponent implements OnChanges {
  private readonly loader = inject(MONACO_LOADER);
  private readonly destroyRef = inject(DestroyRef);
  private readonly editorHost = viewChild.required<ElementRef<HTMLDivElement>>('editorHost');
  private editorInstance: editor.IStandaloneCodeEditor | null = null;
  private destroyed = false;

  readonly value = input.required<string>();
  readonly ariaLabel = input('C++17 source code editor');
  readonly valueChange = output<string>();
  protected readonly ready = signal(false);
  protected readonly loadFailed = signal(false);

  constructor() {
    afterNextRender(() => void this.initialize());
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.editorInstance?.dispose();
    });
  }

  ngOnChanges(): void {
    if (this.editorInstance !== null && this.editorInstance.getValue() !== this.value()) {
      this.editorInstance.setValue(this.value());
    }
  }

  protected updateFallback(event: Event): void {
    this.valueChange.emit((event.target as HTMLTextAreaElement).value);
  }

  private async initialize(): Promise<void> {
    try {
      const monaco = await this.loader();
      if (this.destroyed) return;
      this.editorInstance = monaco.editor.create(this.editorHost().nativeElement, {
        value: this.value(),
        language: 'cpp',
        theme: 'vs-dark',
        ariaLabel: this.ariaLabel(),
        automaticLayout: true,
        fontFamily: 'var(--font-mono)',
        fontSize: 14,
        minimap: { enabled: false },
        padding: { top: 14 },
        scrollBeyondLastLine: false,
        tabSize: 2,
      });
      this.editorInstance.onDidChangeModelContent(() => {
        if (this.editorInstance !== null) this.valueChange.emit(this.editorInstance.getValue());
      });
      this.ready.set(true);
    } catch {
      this.loadFailed.set(true);
    }
  }
}
