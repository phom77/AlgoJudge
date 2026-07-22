namespace AlgoJudge.Infrastructure.ContentGeneration;

internal static class DotNetSourceGenerationHost
{
    public const string Project = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net10.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
            <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
          </PropertyGroup>
          <ItemGroup>
            <Reference Include="AlgoJudge.ProblemGeneratorSdk">
              <HintPath>/sdk/AlgoJudge.ProblemGeneratorSdk.dll</HintPath>
              <Private>true</Private>
            </Reference>
          </ItemGroup>
        </Project>
        """;

    public const string Source = """
        global using AlgoJudge.ProblemGeneratorSdk;
        global using System.Text.Json;

        using System.Reflection;
        using System.Text.Json.Serialization;

        internal static class HostProgram
        {
            private static readonly JsonSerializerOptions JsonOptions = new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = false,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            };

            public static int Main()
            {
                var requestJson = Console.In.ReadToEnd();
                var request = JsonSerializer.Deserialize<HostRequest>(requestJson, JsonOptions)
                    ?? throw new InvalidOperationException("Generation request is empty.");
                var generator = CreateSingle<ProblemGenerator>();
                var validator = CreateSingle<InputValidator>();
                var plan = new TestPlan();
                generator.Build(plan);

                var output = new List<HostCase>();
                foreach (var handwritten in request.HandwrittenCases)
                {
                    using var document = JsonDocument.Parse(handwritten.ArgumentsJson);
                    var arguments = document.RootElement.Clone();
                    Validate(validator, arguments, handwritten.Name);
                    output.Add(new HostCase(
                        output.Count + 1,
                        handwritten.Name,
                        handwritten.Group,
                        0,
                        arguments.GetRawText()));
                }

                var generated = plan.Execute(
                    request.RootSeed,
                    request.MaximumCaseCount - output.Count);
                foreach (var testCase in generated)
                {
                    if (testCase.Arguments.Values.Count != request.ParameterNames.Count)
                    {
                        throw new InvalidOperationException(
                            $"Case {testCase.Name} returned {testCase.Arguments.Values.Count} arguments; " +
                            $"the signature requires {request.ParameterNames.Count}.");
                    }

                    var values = new Dictionary<string, object?>(StringComparer.Ordinal);
                    for (var index = 0; index < request.ParameterNames.Count; index++)
                        values.Add(request.ParameterNames[index], testCase.Arguments.Values[index]);
                    var arguments = JsonSerializer.SerializeToElement(values, JsonOptions);
                    Validate(validator, arguments, testCase.Name);
                    output.Add(new HostCase(
                        output.Count + 1,
                        testCase.Name,
                        testCase.Group,
                        testCase.Seed,
                        arguments.GetRawText()));
                }

                Console.Out.Write(JsonSerializer.Serialize(new HostResponse(output), JsonOptions));
                return 0;
            }

            private static T CreateSingle<T>() where T : class
            {
                var candidates = Assembly.GetExecutingAssembly()
                    .GetTypes()
                    .Where(type => !type.IsAbstract && typeof(T).IsAssignableFrom(type))
                    .ToArray();
                if (candidates.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Authoring source must declare exactly one concrete {typeof(T).Name}.");
                }
                return (T)(Activator.CreateInstance(candidates[0]) ??
                    throw new InvalidOperationException($"Could not create {typeof(T).Name}."));
            }

            private static void Validate(InputValidator validator, JsonElement arguments, string name)
            {
                var result = validator.Validate(arguments) ??
                    throw new InvalidOperationException($"Validator returned null for case {name}.");
                if (!result.IsValid)
                {
                    var reason = string.IsNullOrWhiteSpace(result.Error)
                        ? "no reason supplied"
                        : result.Error;
                    throw new InvalidOperationException(
                        $"Input validation failed for case {name}: {reason}.");
                }
            }

            private sealed record HostRequest(
                int RootSeed,
                int MaximumCaseCount,
                IReadOnlyList<string> ParameterNames,
                IReadOnlyList<HostHandwrittenCase> HandwrittenCases);

            private sealed record HostHandwrittenCase(
                string Name,
                string Group,
                string ArgumentsJson);

            private sealed record HostCase(
                int Ordinal,
                string Name,
                string Group,
                int Seed,
                string Input);

            private sealed record HostResponse(IReadOnlyList<HostCase> Cases);
        }
        """;
}
