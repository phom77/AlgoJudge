import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';

import { ProblemCataloguePage } from './problem-catalogue.page';

describe('ProblemCataloguePage', () => {
  let fixture: ComponentFixture<ProblemCataloguePage>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProblemCataloguePage],
    }).compileComponents();

    fixture = TestBed.createComponent(ProblemCataloguePage);
    await fixture.whenStable();
  });

  it('communicates that the catalogue has no embedded mock API data', () => {
    const element = fixture.nativeElement as HTMLElement;

    expect(element.textContent).toContain('No mock API data is embedded in the scaffold.');
  });
});
