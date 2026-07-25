import { FormArray, FormControl, FormGroup, Validators } from '@angular/forms';

import type { FunctionValueTypeName } from './data-access/authoring.models';

const identifier = /^[A-Za-z_][A-Za-z0-9_]*$/;

export function createMetadataForm() {
  return new FormGroup({
    slug: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)],
    }),
    title: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(255)],
    }),
    statementMarkdown: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    constraintsMarkdown: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    difficulty: new FormControl<'Easy' | 'Medium' | 'Hard'>('Easy', { nonNullable: true }),
    timeLimitMs: new FormControl(1000, {
      nonNullable: true,
      validators: [Validators.min(100), Validators.max(10000)],
    }),
    memoryLimitKb: new FormControl(262144, {
      nonNullable: true,
      validators: [Validators.min(16384), Validators.max(1048576)],
    }),
    sampleInput: new FormControl('{"values":[2,7,11,15],"target":9}', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    sampleOutput: new FormControl('[0,1]', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });
}

export function createSignatureForm() {
  return new FormGroup({
    className: new FormControl('Solution', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(identifier)],
    }),
    methodName: new FormControl('twoSum', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(identifier)],
    }),
    returnType: new FormControl<FunctionValueTypeName>('Int32Array', { nonNullable: true }),
    parameters: new FormArray([
      createParameterControl('values', 'Int32Array'),
      createParameterControl('target', 'Int32'),
    ]),
  });
}

export function createParameterControl(name = '', type: FunctionValueTypeName = 'Int32') {
  return new FormGroup({
    name: new FormControl(name, {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(identifier)],
    }),
    type: new FormControl<FunctionValueTypeName>(type, { nonNullable: true }),
  });
}

export function createCasesForm() {
  return new FormGroup({ cases: new FormArray([createCaseControl()]) });
}

export function createCaseControl(name = 'minimum', argumentsJson = '{"values":[2,7],"target":9}') {
  return new FormGroup({
    name: new FormControl(name, {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)],
    }),
    argumentsJson: new FormControl(argumentsJson, {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });
}

export function createSourcesForm(
  generator: string,
  validator: string,
  reference: string,
  wrong: string,
) {
  return new FormGroup({
    generator: new FormControl(generator, { nonNullable: true, validators: [Validators.required] }),
    validator: new FormControl(validator, { nonNullable: true, validators: [Validators.required] }),
    referenceSolution: new FormControl(reference, {
      nonNullable: true,
      validators: [Validators.required],
    }),
    wrongSolutionName: new FormControl('adjacent-only', {
      nonNullable: true,
      validators: [Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)],
    }),
    wrongSolution: new FormControl(wrong, { nonNullable: true }),
  });
}
