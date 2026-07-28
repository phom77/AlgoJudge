using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AlgoJudge.Application.ContentGeneration;
using AlgoJudge.Application.FunctionExecution;
using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.ContentTool.Generation;
using AlgoJudge.ContentTool.Packages;
using AlgoJudge.Domain.Enums;
using AlgoJudge.Domain.Execution;

namespace AlgoJudge.ContentTool.Workspace;

public sealed partial class ContentWorkspaceResolver
{
    private const int PlatformTimeLimitMs = 1_000;
    private const int PlatformMemoryLimitKb = 262_144;
    private const int PlatformGeneratorSdkVersion = 1;
    private const string PlatformLanguage = "cpp17";

    private static readonly IReadOnlyCollection<string> CatalogRequiredProperties =
        ["schemaVersion", "problems"];
    private static readonly IReadOnlyCollection<string> ProblemRequiredProperties =
        ["schemaVersion", "template", "slug", "title", "difficulty", "tags", "statement",
         "constraints", "signature", "samples", "generatorParameters"];
    private static readonly IReadOnlyCollection<string> TemplateRequiredProperties =
        ["schemaVersion", "generatorParametersSchema"];
    private static readonly HashSet<string> Actions =
        ["create", "update-draft", "new-revision", "skip"];
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly ContentImportOptions _options;

    public ContentWorkspaceResolver(ContentImportOptions options)
    {
        options.Validate();
        _options = options;
    }

    public async Task<ContentWorkspaceResolution> ResolveAsync(
        string catalogPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ResolveCoreAsync(catalogPath, cancellationToken);
        }
        catch (WorkspaceValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            IOException or
            NotSupportedException or
            UnauthorizedAccessException)
        {
            throw WorkspaceJson.Error("The content workspace could not be read safely.");
        }
    }

    private async Task<ContentWorkspaceResolution> ResolveCoreAsync(
        string catalogPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        var fullCatalogPath = Path.GetFullPath(catalogPath);
        if (!File.Exists(fullCatalogPath))
            throw WorkspaceJson.Error($"Catalog does not exist: {fullCatalogPath}.");

        var workspaceRoot = Path.GetDirectoryName(fullCatalogPath)!;
        var paths = new WorkspacePathResolver(workspaceRoot);
        paths.EnsureContained(new FileInfo(fullCatalogPath), "Catalog path");
        var (catalog, _) = await WorkspaceJson.ReadAsync<ContentCatalog>(
            fullCatalogPath,
            "catalog.json",
            _options.MaxEntryBytes,
            CatalogRequiredProperties,
            cancellationToken);

        var catalogErrors = ValidateCatalog(catalog);
        if (catalogErrors.Count > 0)
            throw new WorkspaceValidationException(catalogErrors);

        var resolved = new List<ResolvedWorkspaceProblem>();
        var errors = new List<string>();
        foreach (var entry in catalog.Problems.Where(problem => problem.Enabled))
        {
            try
            {
                resolved.Add(await ResolveProblemAsync(
                    paths,
                    workspaceRoot,
                    entry,
                    cancellationToken));
            }
            catch (WorkspaceValidationException exception)
            {
                errors.AddRange(exception.Errors.Select(error => $"{entry.Path}: {error}"));
            }
        }

        foreach (var duplicate in resolved
                     .GroupBy(problem => problem.Metadata.Slug, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Duplicate problem slug in enabled catalog entries: {duplicate.Key}.");
        }

        if (errors.Count > 0)
            throw new WorkspaceValidationException(errors);

        return new ContentWorkspaceResolution
        {
            CatalogPath = NormalizeRelativePath(
                Path.GetRelativePath(Directory.GetCurrentDirectory(), fullCatalogPath)),
            Problems = resolved
        };
    }

    private async Task<ResolvedWorkspaceProblem> ResolveProblemAsync(
        WorkspacePathResolver paths,
        string workspaceRoot,
        ContentCatalogProblem entry,
        CancellationToken cancellationToken)
    {
        var problemDirectory = paths.ResolveDirectory(entry.Path, "Problem path");
        var problemPath = paths.ResolveRequiredFile(
            problemDirectory,
            "problem.json",
            "problem.json");
        var (problem, problemRoot) = await WorkspaceJson.ReadAsync<ContentProblemManifest>(
            problemPath,
            "problem.json",
            _options.MaxEntryBytes,
            ProblemRequiredProperties,
            cancellationToken);
        var errors = ValidateProblem(problem, problemRoot);
        if (errors.Count > 0)
            throw new WorkspaceValidationException(errors);

        var templateDirectory = paths.ResolveDirectory(
            $"templates/{problem.Template}",
            $"Template '{problem.Template}'");
        var templatePath = paths.ResolveRequiredFile(
            templateDirectory,
            "template.json",
            $"Template '{problem.Template}' template.json");
        var (template, _) = await WorkspaceJson.ReadAsync<ContentTemplateManifest>(
            templatePath,
            $"Template '{problem.Template}' template.json",
            _options.MaxEntryBytes,
            TemplateRequiredProperties,
            cancellationToken);
        ValidateTemplate(template);
        GeneratorParametersSchemaValidator.ValidateSchema(template.GeneratorParametersSchema);
        var generatorParameters = GeneratorParametersSchemaValidator.ResolveAndValidate(
            template.GeneratorParametersSchema,
            problem.GeneratorParameters);

        var generatorTemplatePath = paths.ResolveRequiredFile(
            templateDirectory,
            "generator.cs",
            $"Template '{problem.Template}' generator.cs");
        var validatorTemplatePath = paths.ResolveRequiredFile(
            templateDirectory,
            "validator.cs",
            $"Template '{problem.Template}' validator.cs");
        var generatorOverridePath = paths.ResolveOptionalFile(
            problemDirectory,
            "generator.cs",
            "Problem generator.cs");
        var validatorOverridePath = paths.ResolveOptionalFile(
            problemDirectory,
            "validator.cs",
            "Problem validator.cs");
        var referencePath = paths.ResolveRequiredFile(
            problemDirectory,
            "reference.cpp",
            "Problem reference.cpp");

        var generatorPath = generatorOverridePath ?? generatorTemplatePath;
        var validatorPath = validatorOverridePath ?? validatorTemplatePath;
        var generatorSource = await ReadSourceAsync(
            generatorPath,
            "Generator source",
            cancellationToken);
        var validatorSource = await ReadSourceAsync(
            validatorPath,
            "Input validator source",
            cancellationToken);
        var referenceSource = await ReadSourceAsync(
            referencePath,
            "Reference solution source",
            cancellationToken);
        var wrongSolutions = await ReadWrongSolutionsAsync(
            paths,
            problemDirectory,
            cancellationToken);

        var sdkVersion = template.GeneratorSdkVersion ?? PlatformGeneratorSdkVersion;
        var qualityPolicy = problem.QualityPolicy ?? template.QualityPolicy ?? new SuiteQualityPolicy();
        var definition = new ProblemAuthoringDefinition
        {
            SchemaVersion = 1,
            ExecutionMode = template.ExecutionMode ?? ProblemExecutionMode.Function,
            FunctionSignature = problem.Signature,
            HandwrittenCases = problem.Samples
                .Select((sample, index) => new HandwrittenCaseDefinition
                {
                    Name = $"sample-{index + 1:D2}",
                    Group = "handwritten",
                    Arguments = sample.Arguments.Clone()
                })
                .ToArray(),
            Generator = new GeneratorSourceDefinition
            {
                Language = "csharp",
                SdkVersion = sdkVersion,
                Source = generatorSource
            },
            InputValidator = new GeneratorSourceDefinition
            {
                Language = "csharp",
                SdkVersion = sdkVersion,
                Source = validatorSource
            },
            ReferenceSolution = new FunctionSourceDefinition
            {
                Language = PlatformLanguage,
                Source = referenceSource
            },
            WrongSolutions = wrongSolutions.Select(item => item.Definition).ToArray(),
            QualityPolicy = qualityPolicy
        };

        var metadata = new ResolvedProblemMetadata
        {
            Slug = problem.Slug,
            Title = problem.Title,
            Difficulty = problem.Difficulty,
            Tags = problem.Tags,
            Statement = problem.Statement,
            Constraints = problem.Constraints,
            TimeLimitMs = problem.TimeLimitMs ?? template.TimeLimitMs ?? PlatformTimeLimitMs,
            MemoryLimitKb = problem.MemoryLimitKb ?? template.MemoryLimitKb ?? PlatformMemoryLimitKb,
            Language = template.Language ?? PlatformLanguage,
            OutputChecker = OutputCheckerConfiguration.JsonExact,
            Samples = problem.Samples
        };
        var sourceOrigins = new ResolvedSourceOrigins
        {
            Generator = ToWorkspaceRelative(workspaceRoot, generatorPath),
            InputValidator = ToWorkspaceRelative(workspaceRoot, validatorPath),
            ReferenceSolution = ToWorkspaceRelative(workspaceRoot, referencePath),
            WrongSolutions = wrongSolutions
                .Select(item => ToWorkspaceRelative(workspaceRoot, item.Path))
                .ToArray()
        };
        var hashPayload = new
        {
            schemaVersion = 1,
            metadata,
            generatorParameters,
            definition
        };
        var contentHash = ContentHash.Sha256(CanonicalJson.Serialize(hashPayload));

        return new ResolvedWorkspaceProblem
        {
            CatalogPath = entry.Path,
            Action = entry.Action,
            Template = problem.Template,
            Metadata = metadata,
            GeneratorParameters = generatorParameters,
            Definition = definition,
            SourceOrigins = sourceOrigins,
            ContentHash = contentHash
        };
    }

    private List<string> ValidateCatalog(ContentCatalog catalog)
    {
        var errors = new List<string>();
        if (catalog.SchemaVersion != 1)
            errors.Add("catalog.json schemaVersion must be 1.");
        if (catalog.Problems is null || catalog.Problems.Count == 0)
            errors.Add("catalog.json must contain at least one problem.");

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in catalog.Problems ?? [])
        {
            if (item is null)
            {
                errors.Add("catalog.json problem entries cannot be null.");
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.Path))
                errors.Add("Catalog problem path is required.");
            else if (!IsSafeRelativePath(item.Path))
                errors.Add($"Catalog problem path is unsafe: {item.Path}.");
            else if (!paths.Add(item.Path))
                errors.Add($"Duplicate catalog problem path: {item.Path}.");
            if (!Actions.Contains(item.Action))
                errors.Add($"Catalog action is invalid for {item.Path}.");
        }
        return errors;
    }

    private List<string> ValidateProblem(ContentProblemManifest problem, JsonElement root)
    {
        var errors = new List<string>();
        if (problem.SchemaVersion != 1)
            errors.Add("problem.json schemaVersion must be 1.");
        if (!NamePattern().IsMatch(problem.Template))
            errors.Add("Problem template must use lowercase kebab-case.");
        if (!NamePattern().IsMatch(problem.Slug) || problem.Slug.Length > 160)
            errors.Add("Problem slug is invalid.");
        if (string.IsNullOrWhiteSpace(problem.Title) || problem.Title.Length > 255)
            errors.Add("Problem title must contain 1-255 characters.");
        if (!Enum.IsDefined(problem.Difficulty))
            errors.Add("Problem difficulty is invalid.");
        if (problem.Tags is null || problem.Tags.Count > 10 ||
            problem.Tags.Any(tag => !NamePattern().IsMatch(tag) || tag.Length > 80) ||
            problem.Tags.Distinct(StringComparer.Ordinal).Count() != problem.Tags.Count)
        {
            errors.Add("Problem tags must be unique lowercase kebab-case values (maximum 10).");
        }
        if (string.IsNullOrWhiteSpace(problem.Statement))
            errors.Add("Problem statement is required.");
        if (problem.Constraints is null || problem.Constraints.Count == 0 ||
            problem.Constraints.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add("Problem constraints must contain at least one non-empty value.");
        }
        if (problem.Samples is null || problem.Samples.Count == 0)
            errors.Add("Problem samples must contain at least one sample.");
        else if (problem.Samples.Count > _options.MaxSampleCount)
            errors.Add($"Problem samples exceed the {_options.MaxSampleCount}-sample limit.");

        ValidateLimit(
            problem.TimeLimitMs,
            _options.MinTimeLimitMs,
            _options.MaxTimeLimitMs,
            "timeLimitMs",
            errors);
        ValidateLimit(
            problem.MemoryLimitKb,
            _options.MinMemoryLimitKb,
            _options.MaxMemoryLimitKb,
            "memoryLimitKb",
            errors);

        var signatureErrors = new List<string>();
        var signature = FunctionPackageValidator.ParseSignature(
            root.GetProperty("signature").GetRawText(),
            signatureErrors);
        errors.AddRange(signatureErrors.Select(error => $"signature: {error}"));
        if (signature is not null)
        {
            var samples = problem.Samples ?? [];
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                ValidateSample(signature, sample, index + 1, errors);
            }
        }

        if (problem.QualityPolicy is not null)
        {
            try
            {
                problem.QualityPolicy.Validate();
            }
            catch (ArgumentException)
            {
                errors.Add("Problem qualityPolicy is invalid.");
            }
        }
        return errors;
    }

    private void ValidateTemplate(ContentTemplateManifest template)
    {
        var errors = new List<string>();
        if (template.SchemaVersion != 1)
            errors.Add("template.json schemaVersion must be 1.");
        if (template.ExecutionMode is not null &&
            template.ExecutionMode != ProblemExecutionMode.Function)
        {
            errors.Add("template.json executionMode must be Function.");
        }
        if (template.Language is not null &&
            !string.Equals(template.Language, PlatformLanguage, StringComparison.Ordinal))
        {
            errors.Add("template.json language must be cpp17.");
        }
        if (template.GeneratorSdkVersion is not null &&
            template.GeneratorSdkVersion != PlatformGeneratorSdkVersion)
        {
            errors.Add("template.json generatorSdkVersion must be 1.");
        }
        ValidateLimit(
            template.TimeLimitMs,
            _options.MinTimeLimitMs,
            _options.MaxTimeLimitMs,
            "template timeLimitMs",
            errors);
        ValidateLimit(
            template.MemoryLimitKb,
            _options.MinMemoryLimitKb,
            _options.MaxMemoryLimitKb,
            "template memoryLimitKb",
            errors);
        if (template.QualityPolicy is not null)
        {
            try
            {
                template.QualityPolicy.Validate();
            }
            catch (ArgumentException)
            {
                errors.Add("template.json qualityPolicy is invalid.");
            }
        }
        if (errors.Count > 0)
            throw new WorkspaceValidationException(errors);
    }

    private static void ValidateSample(
        FunctionSignature signature,
        ContentProblemSample sample,
        int ordinal,
        ICollection<string> errors)
    {
        if (sample is null)
        {
            errors.Add($"Sample {ordinal} cannot be null.");
            return;
        }
        if (sample.Arguments.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            errors.Add($"Sample {ordinal} arguments must be an object.");
        }
        else
        {
            var arguments = sample.Arguments.EnumerateObject().ToArray();
            if (arguments.Length != signature.Parameters.Count)
                errors.Add($"Sample {ordinal} must contain every argument exactly once.");
            foreach (var parameter in signature.Parameters)
            {
                if (!sample.Arguments.TryGetProperty(parameter.Name, out var value))
                    errors.Add($"Sample {ordinal} is missing argument {parameter.Name}.");
                else if (!FunctionValueJsonValidator.Matches(value, parameter.Type))
                    errors.Add($"Sample {ordinal} argument {parameter.Name} has the wrong type.");
            }
            foreach (var argument in arguments)
            {
                if (!signature.Parameters.Any(parameter => parameter.Name == argument.Name))
                    errors.Add($"Sample {ordinal} contains unknown argument {argument.Name}.");
            }
        }
        if (!FunctionValueJsonValidator.Matches(sample.Expected, signature.ReturnType))
            errors.Add($"Sample {ordinal} expected value has the wrong type.");
    }

    private async Task<IReadOnlyList<ResolvedWrongSolution>> ReadWrongSolutionsAsync(
        WorkspacePathResolver paths,
        string problemDirectory,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(problemDirectory, "wrong-solutions");
        if (!Directory.Exists(directory))
            return [];
        paths.EnsureContained(new DirectoryInfo(directory), "wrong-solutions directory");

        var results = new List<ResolvedWrongSolution>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => string.Equals(
                         Path.GetExtension(path),
                         ".cpp",
                         StringComparison.Ordinal))
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            paths.EnsureContained(new FileInfo(path), $"Wrong solution {Path.GetFileName(path)}");
            var name = Path.GetFileNameWithoutExtension(path);
            if (!NamePattern().IsMatch(name))
                throw WorkspaceJson.Error(
                    $"Wrong-solution name must use lowercase kebab-case: {name}.");
            if (!names.Add(name))
                throw WorkspaceJson.Error($"Duplicate wrong-solution name: {name}.");
            results.Add(new ResolvedWrongSolution(
                new WrongSolutionDefinition
                {
                    Name = name,
                    Language = PlatformLanguage,
                    Source = await ReadSourceAsync(path, $"Wrong solution {name}", cancellationToken)
                },
                path));
        }
        return results;
    }

    private async Task<string> ReadSourceAsync(
        string path,
        string description,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > _options.MaxEntryBytes)
            throw WorkspaceJson.Error($"{description} exceeds the {_options.MaxEntryBytes}-byte limit.");
        try
        {
            var source = await File.ReadAllTextAsync(path, StrictUtf8, cancellationToken);
            if (string.IsNullOrWhiteSpace(source))
                throw WorkspaceJson.Error($"{description} cannot be empty.");
            if (source.Contains('\0'))
                throw WorkspaceJson.Error($"{description} contains a null character.");
            return source;
        }
        catch (DecoderFallbackException)
        {
            throw WorkspaceJson.Error($"{description} is not valid UTF-8.");
        }
    }

    private static void ValidateLimit(
        int? value,
        int minimum,
        int maximum,
        string name,
        ICollection<string> errors)
    {
        if (value is not null && (value < minimum || value > maximum))
            errors.Add($"{name} must be between {minimum} and {maximum}.");
    }

    private static string ToWorkspaceRelative(string workspaceRoot, string path) =>
        NormalizeRelativePath(Path.GetRelativePath(workspaceRoot, path));

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    private static bool IsSafeRelativePath(string path)
    {
        if (Path.IsPathRooted(path) ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            WindowsDrivePath().IsMatch(path) ||
            path.Contains('\\'))
        {
            return false;
        }

        return path.Split('/').All(segment =>
            segment.Length > 0 && segment is not ("." or ".."));
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.NonBacktracking)]
    private static partial Regex NamePattern();

    [GeneratedRegex("^[A-Za-z]:", RegexOptions.NonBacktracking)]
    private static partial Regex WindowsDrivePath();

    private sealed record ResolvedWrongSolution(
        WrongSolutionDefinition Definition,
        string Path);
}
