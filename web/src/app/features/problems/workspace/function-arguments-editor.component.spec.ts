import { TestBed } from '@angular/core/testing';
import type { ComponentFixture } from '@angular/core/testing';

import { FunctionArgumentsEditorComponent } from './function-arguments-editor.component';

describe('FunctionArgumentsEditorComponent', () => {
  let fixture: ComponentFixture<FunctionArgumentsEditorComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FunctionArgumentsEditorComponent],
    }).compileComponents();
    fixture = TestBed.createComponent(FunctionArgumentsEditorComponent);
    fixture.componentRef.setInput('parameters', [
      { name: 'nums', type: 'Int32Array' },
      { name: 'target', type: 'Int32' },
    ]);
    fixture.detectChanges();
  });

  it('emits named typed arguments parsed from JSON', () => {
    const emitted = vi.fn();
    fixture.componentInstance.argumentsChange.subscribe(emitted);
    setInput(0, '[1, 2]');
    setInput(1, '3');

    expect(emitted).toHaveBeenLastCalledWith({ nums: [1, 2], target: 3 });
  });

  it('emits null and renders an error for a value outside the declared type', () => {
    const emitted = vi.fn();
    fixture.componentInstance.argumentsChange.subscribe(emitted);
    setInput(1, '3.5');
    fixture.detectChanges();

    expect(emitted).toHaveBeenLastCalledWith(null);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain(
      'Enter valid JSON matching Int32.',
    );
  });

  function setInput(index: number, value: string): void {
    const input = (fixture.nativeElement as HTMLElement).querySelectorAll('input')[index];
    if (!(input instanceof HTMLInputElement)) throw new Error('Expected argument input.');
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }
});
