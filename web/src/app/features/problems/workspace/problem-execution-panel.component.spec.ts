import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import type { ProblemDetail } from '../data-access/problem.models';
import { ProblemExecutionPanelComponent } from './problem-execution-panel.component';

describe('ProblemExecutionPanelComponent', () => {
  let fixture: ComponentFixture<ProblemExecutionPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemExecutionPanelComponent],
      providers: [provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(ProblemExecutionPanelComponent);
    fixture.componentRef.setInput('problem', problem);
    fixture.componentRef.setInput('authenticated', true);
    fixture.componentRef.setInput('sourceValid', true);
    fixture.componentRef.setInput('sourceBytes', 128);
    fixture.detectChanges();
  });

  it('keeps run and submit actions available beside the collapsible console', () => {
    const host = fixture.nativeElement as HTMLElement;

    expect(actionButton('Run Code')).not.toBeNull();
    expect(actionButton('Submit')).not.toBeNull();
    expect(host.textContent).toContain('128 / 65,536 bytes');

    button('Collapse console')?.click();
    fixture.detectChanges();

    expect(host.querySelector('.console-content')?.hasAttribute('hidden')).toBe(true);
    expect(actionButton('Run Code')).not.toBeNull();
    expect(actionButton('Submit')).not.toBeNull();
  });

  it('supports arrow, Home, and End navigation across execution tabs', () => {
    const testcaseTab = tab('Testcase');
    testcaseTab.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }));
    fixture.detectChanges();

    const submitTab = tab('Submit');
    expect(submitTab.getAttribute('aria-selected')).toBe('true');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Ready for hidden tests');

    submitTab.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    fixture.detectChanges();

    expect(testcaseTab.getAttribute('aria-selected')).toBe('true');
  });

  it('expands the console and emits the selected action', () => {
    const runRequested = vi.fn();
    const submitRequested = vi.fn();
    fixture.componentInstance.runRequested.subscribe(runRequested);
    fixture.componentInstance.submitRequested.subscribe(submitRequested);

    button('Collapse console')?.click();
    actionButton('Run Code')?.click();
    fixture.detectChanges();
    expect(runRequested).toHaveBeenCalledWith({ input: '' });
    expect(button('Collapse console')).not.toBeNull();

    actionButton('Submit')?.click();
    expect(submitRequested).toHaveBeenCalledOnce();
  });

  function button(name: string): HTMLButtonElement | null {
    return (
      [...(fixture.nativeElement as HTMLElement).querySelectorAll('button')].find(
        (candidate) =>
          candidate.textContent?.trim() === name || candidate.getAttribute('aria-label') === name,
      ) ?? null
    );
  }

  function actionButton(name: string): HTMLButtonElement | null {
    return (
      [
        ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
          'footer button',
        ),
      ].find(
        (candidate) =>
          candidate.textContent?.trim() === name || candidate.getAttribute('aria-label') === name,
      ) ?? null
    );
  }

  function tab(name: string): HTMLButtonElement {
    const candidate = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('[role="tab"]'),
    ].find((element) => element.textContent?.trim() === name);
    if (candidate === undefined) throw new Error(`Expected ${name} tab.`);
    return candidate;
  }
});

const problem: ProblemDetail = {
  id: 7,
  slug: 'two-sum',
  title: 'Two Sum',
  difficulty: 'Easy',
  tags: [{ slug: 'array', name: 'Array' }],
  isSolved: false,
  statementMarkdown: 'Find two values.',
  constraintsMarkdown: '- Use C++17.',
  timeLimitMs: 1_000,
  memoryLimitKb: 262_144,
  judgeVersion: 1,
  executionMode: 'StdinStdout',
  functionSignature: null,
  publishedAt: '2026-07-17T00:00:00Z',
  samples: [],
};
