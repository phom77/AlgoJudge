using System.Text.Json;

namespace AlgoJudge.ProblemGeneratorSdk;

public abstract class InputValidator
{
    public abstract InputValidationResult Validate(JsonElement arguments);
}
