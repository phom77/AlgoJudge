public sealed class TemplateValidator : InputValidator
{
    public override InputValidationResult Validate(JsonElement arguments) =>
        InputValidationResult.Valid;
}
