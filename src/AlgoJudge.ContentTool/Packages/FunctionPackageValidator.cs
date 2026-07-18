using AlgoJudge.Application.FunctionExecution;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AlgoJudge.ContentTool.Packages;

internal static partial class FunctionPackageValidator
{
    private const int MaximumParameterCount = 16;

    private static readonly JsonSerializerOptions SignatureJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private static readonly HashSet<string> Cpp17Keywords = new(
        [
            "alignas", "alignof", "and", "and_eq", "asm", "atomic_cancel",
            "atomic_commit", "atomic_noexcept", "auto", "bitand", "bitor", "bool",
            "break", "case", "catch", "char", "char16_t", "char32_t", "class",
            "compl", "concept", "const", "constexpr", "const_cast", "continue",
            "co_await", "co_return", "co_yield", "decltype", "default", "delete",
            "do", "double", "dynamic_cast", "else", "enum", "explicit", "export",
            "extern", "false", "float", "for", "friend", "goto", "if", "inline",
            "int", "long", "mutable", "namespace", "new", "noexcept", "not",
            "not_eq", "nullptr", "operator", "or", "or_eq", "private", "protected",
            "public", "reflexpr", "register", "reinterpret_cast", "requires", "return",
            "short", "signed", "sizeof", "static", "static_assert", "static_cast",
            "struct", "switch", "synchronized", "template", "this", "thread_local",
            "throw", "true", "try", "typedef", "typeid", "typename", "union",
            "unsigned", "using", "virtual", "void", "volatile", "wchar_t", "while",
            "xor", "xor_eq"
        ],
        StringComparer.Ordinal);

    public static FunctionSignature? ParseSignature(
        string? signatureJson,
        ICollection<string> errors)
    {
        if (signatureJson is null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(signatureJson);
            ValidateDuplicateProperties(document.RootElement, "$", errors);
            ValidateRequiredSignatureProperties(document.RootElement, errors);
            var signature = JsonSerializer.Deserialize<FunctionSignature>(
                signatureJson,
                SignatureJsonOptions);
            ValidateSignature(signature, errors);
            return signature;
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber.HasValue
                ? $" near line {exception.LineNumber.Value + 1}"
                : string.Empty;
            errors.Add($"function/signature.json is invalid{line}.");
            return null;
        }
    }

    public static void ValidateAdapterTemplate(
        string? adapterTemplate,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(adapterTemplate))
        {
            errors.Add("function/adapter-template.cpp cannot be empty.");
            return;
        }

        ValidateSinglePlaceholder(
            adapterTemplate,
            FunctionHarnessPlaceholders.UserSource,
            errors);
        ValidateSinglePlaceholder(
            adapterTemplate,
            FunctionHarnessPlaceholders.ClassName,
            errors);
        ValidateSinglePlaceholder(
            adapterTemplate,
            FunctionHarnessPlaceholders.MethodName,
            errors);

        foreach (Match match in PlaceholderPattern().Matches(adapterTemplate))
        {
            if (match.Value is not (
                    FunctionHarnessPlaceholders.UserSource or
                    FunctionHarnessPlaceholders.ClassName or
                    FunctionHarnessPlaceholders.MethodName))
            {
                errors.Add($"Unknown function adapter placeholder: {match.Value}.");
            }
        }
    }

    public static void ValidateCase(
        FunctionSignature signature,
        string input,
        string output,
        string description,
        ICollection<string> errors)
    {
        try
        {
            using var inputDocument = JsonDocument.Parse(input);
            ValidateDuplicateProperties(inputDocument.RootElement, "$", errors, description);
            ValidateArguments(signature, inputDocument.RootElement, description, errors);
        }
        catch (JsonException)
        {
            errors.Add($"{description} input must be valid JSON.");
        }

        try
        {
            using var outputDocument = JsonDocument.Parse(output);
            ValidateDuplicateProperties(outputDocument.RootElement, "$", errors, description);
            if (!MatchesType(outputDocument.RootElement, signature.ReturnType))
                errors.Add($"{description} output does not match returnType {signature.ReturnType}.");
        }
        catch (JsonException)
        {
            errors.Add($"{description} output must be valid JSON.");
        }
    }

    private static void ValidateRequiredSignatureProperties(
        JsonElement root,
        ICollection<string> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add("function/signature.json root must be an object.");
            return;
        }

        foreach (var name in new[] { "className", "methodName", "returnType", "parameters" })
        {
            if (!root.TryGetProperty(name, out _))
                errors.Add($"function/signature.json requires property {name}.");
        }

        if (!root.TryGetProperty("parameters", out var parameters) ||
            parameters.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var index = 0;
        foreach (var parameter in parameters.EnumerateArray())
        {
            if (parameter.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"function parameter {index + 1} must be an object.");
                index++;
                continue;
            }

            if (!parameter.TryGetProperty("name", out _))
                errors.Add($"function parameter {index + 1} requires property name.");
            if (!parameter.TryGetProperty("type", out _))
                errors.Add($"function parameter {index + 1} requires property type.");
            index++;
        }
    }

    private static void ValidateSignature(
        FunctionSignature? signature,
        ICollection<string> errors)
    {
        if (signature is null)
            return;

        ValidateIdentifier(signature.ClassName, "className", errors);
        ValidateIdentifier(signature.MethodName, "methodName", errors);
        if (!Enum.IsDefined(signature.ReturnType))
            errors.Add("Function returnType is invalid.");

        if (signature.Parameters is null)
        {
            errors.Add("Function parameters cannot be null.");
            return;
        }

        if (signature.Parameters.Count > MaximumParameterCount)
        {
            errors.Add(
                $"A function signature can contain at most {MaximumParameterCount} parameters.");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in signature.Parameters)
        {
            if (parameter is null)
            {
                errors.Add("Function parameters cannot contain null values.");
                continue;
            }

            ValidateIdentifier(parameter.Name, "parameter name", errors);
            if (!string.IsNullOrWhiteSpace(parameter.Name) && !names.Add(parameter.Name))
                errors.Add($"Duplicate function parameter name: {parameter.Name}.");
            if (!Enum.IsDefined(parameter.Type))
                errors.Add($"Function parameter {parameter.Name} has an invalid type.");
        }
    }

    private static void ValidateIdentifier(
        string identifier,
        string description,
        ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            identifier.Length > 100 ||
            !CppIdentifierPattern().IsMatch(identifier) ||
            Cpp17Keywords.Contains(identifier))
        {
            errors.Add($"Function {description} must be a non-keyword C++ identifier.");
        }
    }

    private static void ValidateArguments(
        FunctionSignature signature,
        JsonElement input,
        string description,
        ICollection<string> errors)
    {
        if (input.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{description} input must be a JSON object keyed by parameter name.");
            return;
        }

        var expected = signature.Parameters.ToDictionary(parameter => parameter.Name);
        var actual = input.EnumerateObject().ToArray();
        foreach (var property in actual)
        {
            if (!expected.TryGetValue(property.Name, out var parameter))
            {
                errors.Add($"{description} input contains unknown argument {property.Name}.");
                continue;
            }

            if (!MatchesType(property.Value, parameter.Type))
            {
                errors.Add(
                    $"{description} argument {property.Name} does not match type {parameter.Type}.");
            }
        }

        foreach (var parameter in signature.Parameters)
        {
            if (!input.TryGetProperty(parameter.Name, out _))
                errors.Add($"{description} input is missing argument {parameter.Name}.");
        }
    }

    private static bool MatchesType(JsonElement value, FunctionValueType type) => type switch
    {
        FunctionValueType.Int32 => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        FunctionValueType.Int64 => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        FunctionValueType.Double =>
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetDouble(out var number) &&
            double.IsFinite(number),
        FunctionValueType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        FunctionValueType.String => value.ValueKind == JsonValueKind.String,
        FunctionValueType.Int32Array => MatchesArray(value, FunctionValueType.Int32),
        FunctionValueType.Int64Array => MatchesArray(value, FunctionValueType.Int64),
        FunctionValueType.DoubleArray => MatchesArray(value, FunctionValueType.Double),
        FunctionValueType.BooleanArray => MatchesArray(value, FunctionValueType.Boolean),
        FunctionValueType.StringArray => MatchesArray(value, FunctionValueType.String),
        _ => false
    };

    private static bool MatchesArray(JsonElement value, FunctionValueType itemType) =>
        value.ValueKind == JsonValueKind.Array &&
        value.EnumerateArray().All(item => MatchesType(item, itemType));

    private static void ValidateSinglePlaceholder(
        string template,
        string placeholder,
        ICollection<string> errors)
    {
        var first = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (first < 0 ||
            template.IndexOf(placeholder, first + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            errors.Add(
                $"function/adapter-template.cpp must contain exactly one {placeholder} placeholder.");
        }
    }

    private static void ValidateDuplicateProperties(
        JsonElement element,
        string path,
        ICollection<string> errors,
        string? description = null)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!properties.Add(property.Name))
                {
                    var prefix = description is null ? string.Empty : $"{description} ";
                    errors.Add($"Duplicate JSON property in {prefix}{path}.{property.Name}.");
                }

                ValidateDuplicateProperties(
                    property.Value,
                    $"{path}.{property.Name}",
                    errors,
                    description);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateDuplicateProperties(item, $"{path}[{index}]", errors, description);
                index++;
            }
        }
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.NonBacktracking)]
    private static partial Regex CppIdentifierPattern();

    [GeneratedRegex("\\{\\{[A-Za-z_][A-Za-z0-9_]*\\}\\}", RegexOptions.NonBacktracking)]
    private static partial Regex PlaceholderPattern();
}
