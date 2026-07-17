import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';

import { SafeMarkdownComponent } from './safe-markdown.component';

describe('SafeMarkdownComponent', () => {
  let fixture: ComponentFixture<SafeMarkdownComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [SafeMarkdownComponent] }).compileComponents();
    fixture = TestBed.createComponent(SafeMarkdownComponent);
  });

  it('renders Markdown while Angular removes executable markup', () => {
    fixture.componentRef.setInput(
      'markdown',
      '# Heading\n\n<img src="x" onerror="alert(1)"><script>alert(2)</script>',
    );
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;

    expect(element.querySelector('h1')?.textContent).toBe('Heading');
    expect(element.querySelector('script')).toBeNull();
    expect(element.querySelector('img')?.hasAttribute('onerror')).toBe(false);
  });
});
