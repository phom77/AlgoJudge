import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import type { Submission } from '../data-access/submission.models';
import { SubmissionResultPanelComponent } from './submission-result-panel.component';

describe('SubmissionResultPanelComponent', () => {
  let fixture: ComponentFixture<SubmissionResultPanelComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SubmissionResultPanelComponent],
      providers: [provideRouter([])],
    }).compileComponents();
    fixture = TestBed.createComponent(SubmissionResultPanelComponent);
  });

  it('renders safe generic failure text without hidden-test details', () => {
    fixture.componentRef.setInput('submission', submission('WrongAnswer'));
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Wrong Answer');
    expect(text).toContain('output did not match');
    expect(text).not.toContain('testcase #');
    expect(text).not.toContain('expected output');
  });
});

function submission(status: Submission['status']): Submission {
  return {
    id: '75b27e41-e942-42b1-89dc-4bc087f458c3',
    problemId: 7,
    language: 'cpp17',
    status,
    executionTimeMs: 12,
    memoryUsedKb: 2048,
    createdAt: '2026-07-17T00:00:00Z',
    startedAt: null,
    finishedAt: '2026-07-17T00:00:01Z',
  };
}
