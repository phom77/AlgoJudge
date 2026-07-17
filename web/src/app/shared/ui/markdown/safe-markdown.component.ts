import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  ViewEncapsulation,
} from '@angular/core';
import { marked } from 'marked';

@Component({
  selector: 'aj-safe-markdown',
  template: `<div class="aj-markdown" [innerHTML]="rendered()"></div>`,
  styleUrl: './safe-markdown.component.scss',
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SafeMarkdownComponent {
  readonly markdown = input.required<string>();
  protected readonly rendered = computed(() => {
    const output = marked.parse(this.markdown(), { async: false, gfm: true });
    return typeof output === 'string' ? output : '';
  });
}
