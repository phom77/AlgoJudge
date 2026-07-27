import { isSubmissionIdentifier } from './submission-detail.store';

describe('submission detail identifier validation', () => {
  it('accepts canonical UUIDs regardless of their version', () => {
    expect(isSubmissionIdentifier('0198f26a-9b10-7b67-8e0a-2b3c4d5e6f70')).toBe(true);
    expect(isSubmissionIdentifier('2d74e0cf-8d87-4c18-b9e8-8399a70a928d')).toBe(true);
  });

  it('rejects malformed submission identifiers', () => {
    expect(isSubmissionIdentifier('not-a-guid')).toBe(false);
    expect(isSubmissionIdentifier('2d74e0cf-8d87-4c18-b9e8')).toBe(false);
  });
});
