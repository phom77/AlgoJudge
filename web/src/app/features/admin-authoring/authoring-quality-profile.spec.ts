import type { ProblemAuthoringDefinition } from '../../core/api/admin-generated/models/problem-authoring-definition';
import { qualityPolicyInputFor } from './authoring-quality-profile';

describe('authoring quality profile', () => {
  it('uses the thousand-case Two Sum profile before sources are authored', () => {
    const profile = qualityPolicyInputFor({} as ProblemAuthoringDefinition);

    expect(profile.minimumTestCaseCount).toBe(1000);
    expect(profile.minimumRandomCases).toBe(700);
    expect(profile.minimumStressCases).toBe(50);
  });

  it('preserves an authored definition quality policy', () => {
    const profile = qualityPolicyInputFor({
      generator: { source: 'custom generator' },
      qualityPolicy: {
        minimumTestCaseCount: 32,
        minimumCasesByGroup: [{ group: 'random', minimumCaseCount: 30 }],
        requireEachDeclaredWrongSolutionKilled: false,
      },
    } as ProblemAuthoringDefinition);

    expect(profile.minimumTestCaseCount).toBe(32);
    expect(profile.minimumHandwrittenCases).toBe(1);
    expect(profile.minimumRandomCases).toBe(30);
    expect(profile.requireEachDeclaredWrongSolutionKilled).toBe(false);
  });
});
