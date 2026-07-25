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

import { MONACO_LOADER } from './monaco-loader';

@Component({
  selector: 'aj-source-editor',
  templateUrl: './source-editor.component.html',
  styleUrl: './source-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SourceEditorComponent implements OnChanges {
  private readonly loader = inject(MONACO_LOADER);
  private readonly destroyRef = inject(DestroyRef);
  private readonly host = viewChild.required<ElementRef<HTMLDivElement>>('host');
  private instance: editor.IStandaloneCodeEditor | null = null;
  private destroyed = false;

  readonly value = input.required<string>();
  readonly language = input<'cpp' | 'csharp'>('cpp');
  readonly ariaLabel = input('Source code editor');
  readonly valueChange = output<string>();
  protected readonly ready = signal(false);
  protected readonly failed = signal(false);

  constructor() {
    afterNextRender(() => void this.initialize());
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.instance?.dispose();
    });
  }

  ngOnChanges(): void {
    if (this.instance !== null) {
      if (this.instance.getValue() !== this.value()) this.instance.setValue(this.value());
      const model = this.instance.getModel();
      if (model !== null)
        void this.loader().then((monaco) => monaco.editor.setModelLanguage(model, this.language()));
    }
  }

  protected updateFallback(event: Event): void {
    this.valueChange.emit((event.target as HTMLTextAreaElement).value);
  }

  private async initialize(): Promise<void> {
    try {
      const monaco = await this.loader();
      if (this.destroyed) return;
      this.instance = monaco.editor.create(this.host().nativeElement, {
        value: this.value(),
        language: this.language(),
        theme: 'vs-dark',
        ariaLabel: this.ariaLabel(),
        automaticLayout: true,
        fontFamily: 'var(--font-mono)',
        fontSize: 13,
        minimap: { enabled: false },
        padding: { top: 12 },
        quickSuggestions: true,
        scrollBeyondLastLine: false,
        tabSize: 2,
      });
      this.instance.onDidChangeModelContent(() => {
        if (this.instance !== null) this.valueChange.emit(this.instance.getValue());
      });
      this.ready.set(true);
    } catch {
      this.failed.set(true);
    }
  }
}
