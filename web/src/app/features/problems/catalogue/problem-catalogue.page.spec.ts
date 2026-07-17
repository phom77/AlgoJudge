import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AuthStore } from '../../../core/auth/auth.store';
import { ProblemGateway } from '../data-access/problem.gateway';
import type { ProblemPage } from '../data-access/problem.models';
import { ProblemCataloguePage } from './problem-catalogue.page';

describe('ProblemCataloguePage', () => {
  const isAuthenticated = signal(true);
  const list = vi.fn();
  let fixture: ComponentFixture<ProblemCataloguePage>;

  beforeEach(async () => {
    isAuthenticated.set(true);
    list.mockReset();
    list.mockReturnValue(of(problemPage()));

    await TestBed.configureTestingModule({
      imports: [ProblemCataloguePage],
      providers: [
        provideRouter([]),
        { provide: AuthStore, useValue: { isAuthenticated } },
        { provide: ProblemGateway, useValue: { list } },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ProblemCataloguePage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  });

  it('renders catalogue rows and the authenticated solved indicator', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('Two Sum');
    expect(element.querySelector('a[href="/problems/two-sum"]')).not.toBeNull();
    expect(element.querySelector('[title="Solved"]')).not.toBeNull();
    expect(list).toHaveBeenCalledOnce();
  });

  it('disables progress filtering when the session becomes anonymous', () => {
    isAuthenticated.set(false);
    fixture.detectChanges();
    const progress = fixture.nativeElement.querySelector(
      'select[title="Sign in to filter by progress"]',
    ) as HTMLSelectElement;

    expect(progress.disabled).toBe(true);
  });
});

function problemPage(): ProblemPage {
  return {
    pageNumber: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
    items: [
      {
        id: 1,
        slug: 'two-sum',
        title: 'Two Sum',
        difficulty: 'Easy',
        tags: [{ slug: 'array', name: 'Array' }],
        isSolved: true,
      },
    ],
  };
}
