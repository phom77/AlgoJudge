import type { ProblemAuthoringDefinition } from '../../core/api/admin-generated/models/problem-authoring-definition';
import type { SuiteQualityPolicy } from '../../core/api/admin-generated/models/suite-quality-policy';
import { TWO_SUM_QUALITY_PROFILE } from './authoring-presets';
import type { SuiteQualityPolicyInput } from './data-access/authoring.models';

type QualityGroup = 'handwritten' | 'edge' | 'random' | 'adversarial' | 'stress';

export function qualityPolicyInputFor(
  definition: ProblemAuthoringDefinition | undefined,
): SuiteQualityPolicyInput {
  const useTwoSumScaleProfile = hasNoAuthoringSource(definition);
  const policy = useTwoSumScaleProfile ? undefined : definition?.qualityPolicy;
  return {
    minimumTestCaseCount: numberOrFallback(
      policy?.minimumTestCaseCount,
      useTwoSumScaleProfile ? TWO_SUM_QUALITY_PROFILE.minimumTestCaseCount : 1,
    ),
    minimumHandwrittenCases: groupMinimum(policy, 'handwritten', useTwoSumScaleProfile),
    minimumEdgeCases: groupMinimum(policy, 'edge', useTwoSumScaleProfile),
    minimumRandomCases: groupMinimum(policy, 'random', useTwoSumScaleProfile),
    minimumAdversarialCases: groupMinimum(policy, 'adversarial', useTwoSumScaleProfile),
    minimumStressCases: groupMinimum(policy, 'stress', useTwoSumScaleProfile),
    requireEachDeclaredWrongSolutionKilled:
      policy?.requireEachDeclaredWrongSolutionKilled ??
      TWO_SUM_QUALITY_PROFILE.requireEachDeclaredWrongSolutionKilled,
  };
}

function hasNoAuthoringSource(definition: ProblemAuthoringDefinition | undefined): boolean {
  return (
    !definition?.generator?.source &&
    !definition?.inputValidator?.source &&
    !definition?.referenceSolution?.source
  );
}

function groupMinimum(
  policy: SuiteQualityPolicy | undefined,
  group: QualityGroup,
  useTwoSumScaleProfile: boolean,
): number {
  const configured = policy?.minimumCasesByGroup?.find(
    (item) => item.group === group,
  )?.minimumCaseCount;
  if (configured !== undefined) return numberOrFallback(configured, 0);
  if (!useTwoSumScaleProfile) return group === 'handwritten' ? 1 : 0;
  return twoSumGroupMinimum(group);
}

function twoSumGroupMinimum(group: QualityGroup): number {
  return (
    {
      handwritten: TWO_SUM_QUALITY_PROFILE.minimumHandwrittenCases,
      edge: TWO_SUM_QUALITY_PROFILE.minimumEdgeCases,
      random: TWO_SUM_QUALITY_PROFILE.minimumRandomCases,
      adversarial: TWO_SUM_QUALITY_PROFILE.minimumAdversarialCases,
      stress: TWO_SUM_QUALITY_PROFILE.minimumStressCases,
    }[group] ?? 0
  );
}

function numberOrFallback(value: number | string | undefined, fallback: number): number {
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}
