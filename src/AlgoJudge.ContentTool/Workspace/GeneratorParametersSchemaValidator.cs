using System.Text.Json;

namespace AlgoJudge.ContentTool.Workspace;

internal static class GeneratorParametersSchemaValidator
{
    private static readonly HashSet<string> RootKeywords =
        ["type", "properties", "required", "additionalProperties"];
    private static readonly HashSet<string> ValueKeywords =
        ["type", "minimum", "maximum", "minLength", "maxLength", "minItems", "maxItems",
         "items", "enum", "default"];
    private static readonly HashSet<string> SupportedTypes =
        ["integer", "number", "string", "boolean", "array"];

    public static void ValidateSchema(JsonElement schema)
    {
        var errors = new List<string>();
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw WorkspaceJson.Error(
                "template.json generatorParametersSchema must be an object.");
        }

        RejectUnknownProperties(schema, RootKeywords, "generatorParametersSchema", errors);
        if (!TryGetString(schema, "type", out var rootType) || rootType != "object")
            errors.Add("generatorParametersSchema.type must be object");
        if (!schema.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object)
        {
            errors.Add("generatorParametersSchema.properties must be an object");
        }
        if (!schema.TryGetProperty("additionalProperties", out var additionalProperties) ||
            additionalProperties.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            additionalProperties.GetBoolean())
        {
            errors.Add("generatorParametersSchema.additionalProperties must be false");
        }

        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        if (properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (string.IsNullOrWhiteSpace(property.Name))
                    errors.Add("generator parameter names cannot be empty");
                propertyNames.Add(property.Name);
                ValidateValueSchema(
                    property.Value,
                    $"generatorParametersSchema.properties.{property.Name}",
                    errors);
            }
        }

        if (schema.TryGetProperty("required", out var required))
        {
            if (required.ValueKind != JsonValueKind.Array)
            {
                errors.Add("generatorParametersSchema.required must be an array");
            }
            else
            {
                var requiredNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in required.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(item.GetString()))
                    {
                        errors.Add("generatorParametersSchema.required must contain non-empty strings");
                        continue;
                    }

                    var name = item.GetString()!;
                    if (!requiredNames.Add(name))
                        errors.Add($"duplicate required generator parameter: {name}");
                    if (!propertyNames.Contains(name))
                        errors.Add($"required generator parameter is not declared: {name}");
                }
            }
        }

        ThrowIfInvalid(errors);
    }

    public static JsonElement ResolveAndValidate(JsonElement schema, JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
        {
            throw WorkspaceJson.Error("problem.json generatorParameters must be an object.");
        }

        var errors = new List<string>();
        var properties = schema.GetProperty("properties");
        var declared = properties
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);
        foreach (var property in parameters.EnumerateObject())
        {
            if (!declared.ContainsKey(property.Name))
                errors.Add($"unknown generator parameter: {property.Name}");
        }

        var required = schema.TryGetProperty("required", out var requiredElement)
            ? requiredElement.EnumerateArray()
                .Select(item => item.GetString()!)
                .ToHashSet(StringComparer.Ordinal)
            : [];

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, valueSchema) in declared.OrderBy(
                         pair => pair.Key,
                         StringComparer.Ordinal))
            {
                JsonElement value;
                if (parameters.TryGetProperty(name, out var supplied))
                {
                    value = supplied;
                }
                else if (valueSchema.TryGetProperty("default", out var defaultValue))
                {
                    value = defaultValue;
                }
                else
                {
                    if (required.Contains(name))
                        errors.Add($"required generator parameter is missing: {name}");
                    continue;
                }

                ValidateValue(value, valueSchema, $"generatorParameters.{name}", errors);
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }

        ThrowIfInvalid(errors);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void ValidateValueSchema(
        JsonElement schema,
        string path,
        ICollection<string> errors)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{path} must be an object");
            return;
        }

        RejectUnknownProperties(schema, ValueKeywords, path, errors);
        if (!TryGetString(schema, "type", out var type) ||
            type is null ||
            !SupportedTypes.Contains(type))
        {
            errors.Add($"{path}.type is unsupported");
            return;
        }

        ValidateNonNegativeIntegerKeyword(schema, "minLength", path, errors);
        ValidateNonNegativeIntegerKeyword(schema, "maxLength", path, errors);
        ValidateNonNegativeIntegerKeyword(schema, "minItems", path, errors);
        ValidateNonNegativeIntegerKeyword(schema, "maxItems", path, errors);
        ValidateNumericKeyword(schema, "minimum", path, errors);
        ValidateNumericKeyword(schema, "maximum", path, errors);

        if (type == "array")
        {
            if (!schema.TryGetProperty("items", out var items))
                errors.Add($"{path}.items is required for arrays");
            else
                ValidateValueSchema(items, $"{path}.items", errors);
        }
        else if (schema.TryGetProperty("items", out _))
        {
            errors.Add($"{path}.items is only valid for arrays");
        }

        if (schema.TryGetProperty("minimum", out _) && type is not ("integer" or "number") ||
            schema.TryGetProperty("maximum", out _) && type is not ("integer" or "number"))
        {
            errors.Add($"{path} numeric bounds require integer or number type");
        }
        if ((schema.TryGetProperty("minLength", out _) ||
             schema.TryGetProperty("maxLength", out _)) && type != "string")
        {
            errors.Add($"{path} length bounds require string type");
        }
        if ((schema.TryGetProperty("minItems", out _) ||
             schema.TryGetProperty("maxItems", out _)) && type != "array")
        {
            errors.Add($"{path} item bounds require array type");
        }

        if (TryGetDecimal(schema, "minimum", out var minimum) &&
            TryGetDecimal(schema, "maximum", out var maximum) &&
            minimum > maximum)
        {
            errors.Add($"{path}.minimum cannot exceed maximum");
        }
        if (TryGetInt32(schema, "minLength", out var minLength) &&
            TryGetInt32(schema, "maxLength", out var maxLength) &&
            minLength > maxLength)
        {
            errors.Add($"{path}.minLength cannot exceed maxLength");
        }
        if (TryGetInt32(schema, "minItems", out var minItems) &&
            TryGetInt32(schema, "maxItems", out var maxItems) &&
            minItems > maxItems)
        {
            errors.Add($"{path}.minItems cannot exceed maxItems");
        }

        if (schema.TryGetProperty("enum", out var enumValues))
        {
            if (enumValues.ValueKind != JsonValueKind.Array ||
                enumValues.GetArrayLength() == 0)
            {
                errors.Add($"{path}.enum must be a non-empty array");
            }
            else
            {
                var canonicalValues = new HashSet<string>(StringComparer.Ordinal);
                foreach (var value in enumValues.EnumerateArray())
                {
                    ValidateType(value, type, $"{path}.enum", errors);
                    if (!canonicalValues.Add(value.GetRawText()))
                        errors.Add($"{path}.enum contains duplicate values");
                }
            }
        }

        if (schema.TryGetProperty("default", out var defaultValue))
            ValidateValue(defaultValue, schema, $"{path}.default", errors);
    }

    private static void ValidateValue(
        JsonElement value,
        JsonElement schema,
        string path,
        ICollection<string> errors)
    {
        var type = schema.GetProperty("type").GetString()!;
        if (!ValidateType(value, type, path, errors))
            return;

        if (type is "integer" or "number")
        {
            if (!value.TryGetDecimal(out var number))
            {
                errors.Add($"{path} is outside the supported decimal range");
                return;
            }
            if (TryGetDecimal(schema, "minimum", out var minimum) && number < minimum)
                errors.Add($"{path} is less than minimum {minimum}");
            if (TryGetDecimal(schema, "maximum", out var maximum) && number > maximum)
                errors.Add($"{path} is greater than maximum {maximum}");
        }
        else if (type == "string")
        {
            var length = value.GetString()!.Length;
            if (TryGetInt32(schema, "minLength", out var minimum) && length < minimum)
                errors.Add($"{path} is shorter than minLength {minimum}");
            if (TryGetInt32(schema, "maxLength", out var maximum) && length > maximum)
                errors.Add($"{path} is longer than maxLength {maximum}");
        }
        else if (type == "array")
        {
            var length = value.GetArrayLength();
            if (TryGetInt32(schema, "minItems", out var minimum) && length < minimum)
                errors.Add($"{path} has fewer than minItems {minimum}");
            if (TryGetInt32(schema, "maxItems", out var maximum) && length > maximum)
                errors.Add($"{path} has more than maxItems {maximum}");
            var index = 0;
            foreach (var item in value.EnumerateArray())
                ValidateValue(item, schema.GetProperty("items"), $"{path}[{index++}]", errors);
        }

        if (schema.TryGetProperty("enum", out var enumValues) &&
            !enumValues.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
        {
            errors.Add($"{path} is not one of the allowed values");
        }
    }

    private static bool ValidateType(
        JsonElement value,
        string type,
        string path,
        ICollection<string> errors)
    {
        var valid = type switch
        {
            "integer" => value.ValueKind == JsonValueKind.Number &&
                         value.TryGetInt64(out _),
            "number" => value.ValueKind == JsonValueKind.Number,
            "string" => value.ValueKind == JsonValueKind.String,
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "array" => value.ValueKind == JsonValueKind.Array,
            _ => false
        };
        if (!valid)
            errors.Add($"{path} must be {type}");
        return valid;
    }

    private static void RejectUnknownProperties(
        JsonElement value,
        IReadOnlySet<string> allowed,
        string path,
        ICollection<string> errors)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                errors.Add($"{path} contains unknown keyword {property.Name}");
        }
    }

    private static void ValidateNonNegativeIntegerKeyword(
        JsonElement schema,
        string name,
        string path,
        ICollection<string> errors)
    {
        if (schema.TryGetProperty(name, out var value) &&
            (!value.TryGetInt32(out var number) || number < 0))
        {
            errors.Add($"{path}.{name} must be a non-negative integer");
        }
    }

    private static void ValidateNumericKeyword(
        JsonElement schema,
        string name,
        string path,
        ICollection<string> errors)
    {
        if (schema.TryGetProperty(name, out var value) && !value.TryGetDecimal(out _))
            errors.Add($"{path}.{name} must be a finite JSON number");
    }

    private static bool TryGetString(JsonElement value, string name, out string? result)
    {
        if (value.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            result = property.GetString();
            return true;
        }
        result = null;
        return false;
    }

    private static bool TryGetDecimal(JsonElement value, string name, out decimal result)
    {
        if (value.TryGetProperty(name, out var property))
            return property.TryGetDecimal(out result);
        result = default;
        return false;
    }

    private static bool TryGetInt32(JsonElement value, string name, out int result)
    {
        if (value.TryGetProperty(name, out var property))
            return property.TryGetInt32(out result);
        result = default;
        return false;
    }

    private static void ThrowIfInvalid(ICollection<string> errors)
    {
        if (errors.Count > 0)
            throw new WorkspaceValidationException(errors);
    }
}
