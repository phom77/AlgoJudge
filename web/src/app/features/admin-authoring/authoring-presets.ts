const DUPLICATE_EDGE_CASE_COUNT = 1;
const SMALL_EDGE_CASE_COUNT = 99;
const RANDOM_CASE_COUNT = 700;
const ADVERSARIAL_CASE_COUNT = 149;
const STRESS_CASE_COUNT = 50;

export const GENERATOR_PRESET_CASE_COUNT =
  DUPLICATE_EDGE_CASE_COUNT +
  SMALL_EDGE_CASE_COUNT +
  RANDOM_CASE_COUNT +
  ADVERSARIAL_CASE_COUNT +
  STRESS_CASE_COUNT;

export const TWO_SUM_QUALITY_PROFILE = {
  minimumTestCaseCount: 1_000,
  minimumHandwrittenCases: 1,
  minimumEdgeCases: DUPLICATE_EDGE_CASE_COUNT + SMALL_EDGE_CASE_COUNT,
  minimumRandomCases: RANDOM_CASE_COUNT,
  minimumAdversarialCases: ADVERSARIAL_CASE_COUNT,
  minimumStressCases: STRESS_CASE_COUNT,
  requireEachDeclaredWrongSolutionKilled: true,
} as const;

export const GENERATOR_PRESET = `using AlgoJudge.ProblemGeneratorSdk;

public sealed class Generator : ProblemGenerator
{
    public override void Build(TestPlan plan)
    {
        plan.Edge("duplicate-pair", ${DUPLICATE_EDGE_CASE_COUNT}, _ => Args(new[] { 3, 3 }, 6));
        plan.Edge("small-arrays", ${SMALL_EDGE_CASE_COUNT}, context =>
            UniquePair(context, context.Int(2, 16), 100));
        plan.Random("random-arrays", ${RANDOM_CASE_COUNT}, context =>
            UniquePair(context, context.Int(2, 200), 100000));
        plan.Adversarial("wide-arrays", ${ADVERSARIAL_CASE_COUNT}, context =>
            UniquePair(context, context.Int(201, 2000), 1000000));
        plan.Stress("large-arrays", ${STRESS_CASE_COUNT}, context =>
            UniquePair(context, 10000, 1000000));
    }

    private static TestArguments UniquePair(
        GenerationContext context,
        int length,
        int offsetBound)
    {
        // Before applying the offset there is exactly one zero-sum pair:
        // +magnitude and -magnitude. All remaining values are distinct positives.
        var values = context.Permutations.Generate(length, start: 1);
        var positiveIndex = context.Int(0, length - 1);
        var negativeIndex = context.Int(0, length - 2);
        if (negativeIndex >= positiveIndex)
            negativeIndex++;
        values[negativeIndex] = -values[positiveIndex];

        var offset = context.Int(-offsetBound, offsetBound);
        for (var index = 0; index < values.Length; index++)
            values[index] += offset;
        return Args(values, checked(offset * 2));
    }
}`;

export const VALIDATOR_PRESET = `using System.Text.Json;
using AlgoJudge.ProblemGeneratorSdk;

public sealed class Validator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments)
    {
        var values = arguments.GetProperty("values");
        if (values.ValueKind != JsonValueKind.Array ||
            values.GetArrayLength() is < 2 or > 10000 ||
            !arguments.GetProperty("target").TryGetInt32(out var target))
            return InputValidationResult.Invalid("arguments must match the declared constraints");

        long pairCount = 0;
        var seen = new Dictionary<int, int>();
        foreach (var element in values.EnumerateArray())
        {
            if (!element.TryGetInt32(out var value))
                return InputValidationResult.Invalid("values must contain Int32 elements");
            var complement = (long)target - value;
            if (complement is >= int.MinValue and <= int.MaxValue &&
                seen.TryGetValue((int)complement, out var occurrences))
            {
                pairCount += occurrences;
                if (pairCount > 1)
                    return InputValidationResult.Invalid("exactly one answer must exist");
            }
            seen[value] = seen.GetValueOrDefault(value) + 1;
        }
        return pairCount == 1
            ? InputValidationResult.Valid
            : InputValidationResult.Invalid("exactly one answer must exist");
    }
}`;

export const REFERENCE_PRESET = `#include <vector>
#include <unordered_map>
using namespace std;

class Solution {
public:
    vector<int> twoSum(vector<int> values, int target) {
        unordered_map<int, int> seen;
        for (int i = 0; i < static_cast<int>(values.size()); ++i) {
            auto it = seen.find(target - values[i]);
            if (it != seen.end()) return {it->second, i};
            seen[values[i]] = i;
        }
        return {};
    }
};`;

export const WRONG_SOLUTION_PRESET = `#include <vector>
using namespace std;

class Solution {
public:
    vector<int> twoSum(vector<int> values, int target) {
        for (int i = 0; i + 1 < static_cast<int>(values.size()); ++i)
            if (values[i] + values[i + 1] == target) return {i, i + 1};
        return {};
    }
};`;
