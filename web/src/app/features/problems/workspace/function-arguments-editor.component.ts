import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  EventEmitter,
  inject,
  Input,
  Output,
  signal,
} from '@angular/core';
import type { OnChanges } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormRecord, ReactiveFormsModule } from '@angular/forms';
import type { FunctionParameter } from '../data-access/problem.models';

@Component({
  selector: 'aj-function-arguments-editor',
  imports: [ReactiveFormsModule],
  templateUrl: './function-arguments-editor.component.html',
  styleUrl: './function-arguments-editor.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FunctionArgumentsEditorComponent implements OnChanges {
  private readonly destroyRef = inject(DestroyRef);
  @Input({ required: true }) parameters: readonly FunctionParameter[] = [];
  @Output() readonly argumentsChange = new EventEmitter<Readonly<Record<string, unknown>> | null>();
  protected readonly form = new FormRecord<FormControl<string>>({});
  protected readonly invalidNames = signal<ReadonlySet<string>>(new Set());

  constructor() {
    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.emitValue());
  }

  ngOnChanges(): void {
    const expected = new Set(this.parameters.map((parameter) => parameter.name));
    for (const name of Object.keys(this.form.controls))
      if (!expected.has(name)) this.form.removeControl(name, { emitEvent: false });
    for (const parameter of this.parameters)
      if (!this.form.contains(parameter.name))
        this.form.addControl(
          parameter.name,
          new FormControl(defaultJson(parameter.type), { nonNullable: true }),
          { emitEvent: false },
        );
    this.emitValue();
  }

  protected isInvalid(name: string): boolean {
    return this.invalidNames().has(name);
  }

  private emitValue(): void {
    const result: Record<string, unknown> = {};
    const invalid = new Set<string>();
    for (const parameter of this.parameters) {
      try {
        const value: unknown = JSON.parse(this.form.controls[parameter.name]?.value ?? '');
        if (!matchesType(value, parameter.type)) invalid.add(parameter.name);
        else result[parameter.name] = value;
      } catch {
        invalid.add(parameter.name);
      }
    }
    this.invalidNames.set(invalid);
    this.argumentsChange.emit(invalid.size === 0 ? result : null);
  }
}

function defaultJson(type: FunctionParameter['type']): string {
  if (type.endsWith('Array')) return '[]';
  if (type === 'String') return '""';
  if (type === 'Boolean') return 'false';
  return '0';
}

function matchesType(value: unknown, type: FunctionParameter['type']): boolean {
  if (type.endsWith('Array')) {
    if (!Array.isArray(value)) return false;
    const itemType = type.slice(0, -5) as FunctionParameter['type'];
    return value.every((item) => matchesType(item, itemType));
  }
  if (type === 'String') return typeof value === 'string';
  if (type === 'Boolean') return typeof value === 'boolean';
  if (type === 'Int32')
    return (
      typeof value === 'number' &&
      Number.isInteger(value) &&
      value >= -2147483648 &&
      value <= 2147483647
    );
  if (type === 'Int64') return typeof value === 'number' && Number.isSafeInteger(value);
  return typeof value === 'number' && Number.isFinite(value);
}
