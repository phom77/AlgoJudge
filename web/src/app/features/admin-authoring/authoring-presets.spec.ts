import { describe, expect, it } from 'vitest';

import {
  GENERATOR_PRESET,
  GENERATOR_PRESET_CASE_COUNT,
  TWO_SUM_QUALITY_PROFILE,
  VALIDATOR_PRESET,
} from './authoring-presets';

describe('problem authoring presets', () => {
  it('declares the visible generated-case budget from the source counts', () => {
    expect(GENERATOR_PRESET_CASE_COUNT).toBe(999);
    expect(GENERATOR_PRESET).toContain('plan.Edge("duplicate-pair", 1');
    expect(GENERATOR_PRESET).toContain('plan.Edge("small-arrays", 99');
    expect(GENERATOR_PRESET).toContain('plan.Random("random-arrays", 700');
    expect(GENERATOR_PRESET).toContain('plan.Adversarial("wide-arrays", 149');
    expect(GENERATOR_PRESET).toContain('plan.Stress("large-arrays", 50');
    expect(TWO_SUM_QUALITY_PROFILE.minimumTestCaseCount).toBe(
      GENERATOR_PRESET_CASE_COUNT + TWO_SUM_QUALITY_PROFILE.minimumHandwrittenCases,
    );
  });

  it('constructs one pair and validates that exactly one answer exists', () => {
    expect(GENERATOR_PRESET).toContain('values[negativeIndex] = -values[positiveIndex]');
    expect(GENERATOR_PRESET).toContain('return Args(values, checked(offset * 2))');
    expect(VALIDATOR_PRESET).toContain('pairCount > 1');
    expect(VALIDATOR_PRESET).toContain('pairCount == 1');
  });
});
