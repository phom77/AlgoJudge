namespace AlgoJudge.Application.ContentGeneration;

public interface IInputValidator
{
    Task<InputValidationResult> ValidateAsync(
        string input,
        CancellationToken cancellationToken = default);
}
