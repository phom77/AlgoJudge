using System.Text;
using AlgoJudge.Application.FunctionExecution;

namespace AlgoJudge.Infrastructure.Grading;

public sealed class Cpp17FunctionHarnessBuilder : IFunctionHarnessBuilder
{
    private const int MaximumParameterCount = 16;

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

    public string Build(string sourceCode, FunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ValidateSignature(signature);

        var harness = new StringBuilder(sourceCode.Length + Cpp17FunctionHarnessRuntime.Source.Length + 1024);
        harness.AppendLine("#include <algorithm>");
        harness.AppendLine("#include <array>");
        harness.AppendLine("#include <cerrno>");
        harness.AppendLine("#include <charconv>");
        harness.AppendLine("#include <cmath>");
        harness.AppendLine("#include <cstdint>");
        harness.AppendLine("#include <cstdlib>");
        harness.AppendLine("#include <iomanip>");
        harness.AppendLine("#include <iostream>");
        harness.AppendLine("#include <iterator>");
        harness.AppendLine("#include <limits>");
        harness.AppendLine("#include <sstream>");
        harness.AppendLine("#include <stdexcept>");
        harness.AppendLine("#include <string>");
        harness.AppendLine("#include <string_view>");
        harness.AppendLine("#include <utility>");
        harness.AppendLine("#include <vector>");
        harness.AppendLine("using namespace std;");
        harness.AppendLine(sourceCode);
        harness.AppendLine(Cpp17FunctionHarnessRuntime.Source);
        AppendEntryPoint(harness, signature);
        return harness.ToString();
    }

    public string BuildLegacy(
        string sourceCode,
        FunctionSignature signature,
        string adapterTemplate)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ValidateSignature(signature);
        ArgumentNullException.ThrowIfNull(adapterTemplate);

        EnsureSinglePlaceholder(adapterTemplate, FunctionHarnessPlaceholders.UserSource);
        EnsureSinglePlaceholder(adapterTemplate, FunctionHarnessPlaceholders.ClassName);
        EnsureSinglePlaceholder(adapterTemplate, FunctionHarnessPlaceholders.MethodName);

        return adapterTemplate
            .Replace(FunctionHarnessPlaceholders.ClassName, signature.ClassName, StringComparison.Ordinal)
            .Replace(FunctionHarnessPlaceholders.MethodName, signature.MethodName, StringComparison.Ordinal)
            .Replace(FunctionHarnessPlaceholders.UserSource, sourceCode, StringComparison.Ordinal);
    }

    private static void AppendEntryPoint(StringBuilder harness, FunctionSignature signature)
    {
        harness.AppendLine("int main() {");
        harness.AppendLine("    try {");
        harness.AppendLine("        std::string input((std::istreambuf_iterator<char>(std::cin)), std::istreambuf_iterator<char>());");
        harness.AppendLine("        algojudge_harness::Parser parser(input);");
        harness.AppendLine("        auto root = parser.parse_document();");
        harness.AppendLine($"        root.require_object_size({signature.Parameters.Count});");

        for (var index = 0; index < signature.Parameters.Count; index++)
        {
            var parameter = signature.Parameters[index];
            harness.Append("        auto argument_")
                .Append(index)
                .Append(" = algojudge_harness::")
                .Append(GetReader(parameter.Type))
                .Append("(root.required(\"")
                .Append(parameter.Name)
                .AppendLine("\"));");
        }

        harness.Append("        ")
            .Append(signature.ClassName)
            .AppendLine(" solution;");
        harness.Append("        auto result = solution.")
            .Append(signature.MethodName)
            .Append('(');
        for (var index = 0; index < signature.Parameters.Count; index++)
        {
            if (index > 0)
                harness.Append(", ");
            harness.Append("argument_").Append(index);
        }
        harness.AppendLine(");");
        harness.AppendLine("        std::cout << algojudge_harness::serialize(result);");
        harness.AppendLine("        return 0;");
        harness.AppendLine("    } catch (const std::exception&) {");
        harness.AppendLine("        return 1;");
        harness.AppendLine("    } catch (...) {");
        harness.AppendLine("        return 1;");
        harness.AppendLine("    }");
        harness.AppendLine("}");
    }

    private static string GetReader(FunctionValueType type) => type switch
    {
        FunctionValueType.Int32 => "as_int32",
        FunctionValueType.Int64 => "as_int64",
        FunctionValueType.Double => "as_double",
        FunctionValueType.Boolean => "as_boolean",
        FunctionValueType.String => "as_string",
        FunctionValueType.Int32Array => "as_int32_array",
        FunctionValueType.Int64Array => "as_int64_array",
        FunctionValueType.DoubleArray => "as_double_array",
        FunctionValueType.BooleanArray => "as_boolean_array",
        FunctionValueType.StringArray => "as_string_array",
        _ => throw new ArgumentException($"Unsupported function value type: {type}.", nameof(type))
    };

    private static void ValidateSignature(FunctionSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ValidateIdentifier(signature.ClassName, nameof(signature.ClassName));
        ValidateIdentifier(signature.MethodName, nameof(signature.MethodName));

        if (!Enum.IsDefined(signature.ReturnType))
            throw new ArgumentException("Function return type is invalid.", nameof(signature));
        if (signature.Parameters is null)
            throw new ArgumentException("Function parameters cannot be null.", nameof(signature));
        if (signature.Parameters.Count > MaximumParameterCount)
        {
            throw new ArgumentException(
                $"A function signature can contain at most {MaximumParameterCount} parameters.",
                nameof(signature));
        }

        var parameterNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in signature.Parameters)
        {
            if (parameter is null)
                throw new ArgumentException("Function parameters cannot contain null values.", nameof(signature));

            ValidateIdentifier(parameter.Name, "parameter name");
            if (!parameterNames.Add(parameter.Name))
                throw new ArgumentException($"Duplicate function parameter name: {parameter.Name}.", nameof(signature));
            if (!Enum.IsDefined(parameter.Type))
            {
                throw new ArgumentException(
                    $"Function parameter {parameter.Name} has an invalid type.",
                    nameof(signature));
            }
        }
    }

    private static void ValidateIdentifier(string identifier, string description)
    {
        if (string.IsNullOrWhiteSpace(identifier) ||
            identifier.Length > 100 ||
            !IsCppIdentifier(identifier) ||
            Cpp17Keywords.Contains(identifier))
        {
            throw new ArgumentException(
                $"Function {description} must be a non-keyword C++ identifier.");
        }
    }

    private static bool IsCppIdentifier(string value)
    {
        if (!(value[0] == '_' || IsAsciiLetter(value[0])))
            return false;

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!(character == '_' || IsAsciiLetter(character) || char.IsAsciiDigit(character)))
                return false;
        }

        return true;
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static void EnsureSinglePlaceholder(string template, string placeholder)
    {
        var first = template.IndexOf(placeholder, StringComparison.Ordinal);
        if (first < 0 ||
            template.IndexOf(placeholder, first + placeholder.Length, StringComparison.Ordinal) >= 0)
        {
            throw new ArgumentException(
                $"Function adapter template must contain exactly one {placeholder} placeholder.",
                nameof(template));
        }
    }
}
