import { describe, expect, it } from 'vitest';

import { createCasesForm, createMetadataForm, createSignatureForm } from './authoring.forms';

describe('problem authoring forms', () => {
  it('rejects invalid public metadata before creating a draft', () => {
    const form = createMetadataForm();
    form.patchValue({
      slug: 'Not a slug',
      title: '',
      statementMarkdown: '',
      constraintsMarkdown: '',
    });

    expect(form.invalid).toBe(true);
  });

  it('starts with a function contract and handwritten case that agree', () => {
    const signature = createSignatureForm().getRawValue();
    const testcase = JSON.parse(createCasesForm().getRawValue().cases[0].argumentsJson) as Record<
      string,
      unknown
    >;

    expect(signature.parameters.map((parameter) => parameter.name)).toEqual(['values', 'target']);
    expect(Object.keys(testcase)).toEqual(['values', 'target']);
  });
});
