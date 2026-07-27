import {
  isProblemStatus,
  problemStatusLabel,
  revisionStatusLabel,
} from './problem-management.models';

describe('problem management enum mapping', () => {
  it('maps both numeric and string problem statuses', () => {
    expect(problemStatusLabel(1)).toBe('Draft');
    expect(problemStatusLabel('Published')).toBe('Published');
    expect(problemStatusLabel('archived')).toBe('Archived');
  });

  it('maps both numeric and string revision statuses', () => {
    expect(revisionStatusLabel(2)).toBe('Ready');
    expect(revisionStatusLabel('Ready')).toBe('Ready');
    expect(revisionStatusLabel('Published')).toBe('Published');
  });

  it('matches runtime string statuses returned by the API', () => {
    expect(isProblemStatus('Draft', 'Draft')).toBe(true);
    expect(isProblemStatus(2, 'Published')).toBe(true);
    expect(isProblemStatus('Archived', 'Published')).toBe(false);
  });
});
