using AlgoJudge.ContentTool.Configuration;
using AlgoJudge.Domain.Enums;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AlgoJudge.ContentTool.Packages;

public sealed partial class ProblemPackageReader
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    private static readonly HashSet<string> RequiredRootFiles = new(
        ["problem.json", "statement.md", "constraints.md"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> AllowedDirectories = new(
        ["samples/", "tests/", "function/"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> FunctionFiles = new(
        ["function/signature.json", "function/adapter-template.cpp"],
        StringComparer.Ordinal);

    private readonly ContentImportOptions _options;

    public ProblemPackageReader(ContentImportOptions options)
    {
        options.Validate();
        _options = options;
    }

    public async Task<ProblemPackage> ReadAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new PackageValidationException(["A package path is required."]);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(packagePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new PackageValidationException(["The package path is invalid."]);
        }

        if (!File.Exists(fullPath))
            throw new PackageValidationException(["The package file does not exist."]);

        if (!string.Equals(Path.GetExtension(fullPath), ".zip", StringComparison.OrdinalIgnoreCase))
            errors.Add("The package must be a .zip archive.");

        var archiveLength = new FileInfo(fullPath).Length;
        if (archiveLength > _options.MaxArchiveBytes)
        {
            errors.Add(
                $"The ZIP file exceeds the {_options.MaxArchiveBytes}-byte archive limit.");
        }

        ThrowIfInvalid(errors);

        try
        {
            await using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            return await ReadArchiveAsync(archive, cancellationToken);
        }
        catch (PackageValidationException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new PackageValidationException(
                ["The ZIP archive is corrupt, encrypted, or unsupported."]);
        }
        catch (IOException)
        {
            throw new PackageValidationException(["The ZIP archive could not be read."]);
        }
        catch (UnauthorizedAccessException)
        {
            throw new PackageValidationException(["The ZIP archive cannot be accessed."]);
        }
    }

    private async Task<ProblemPackage> ReadArchiveAsync(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var entries = ValidateEntries(archive, errors);
        ThrowIfInvalid(errors);

        var manifestJson = await ReadRequiredEntryAsync(
            entries,
            "problem.json",
            errors,
            cancellationToken);
        var statement = await ReadRequiredEntryAsync(
            entries,
            "statement.md",
            errors,
            cancellationToken);
        var constraints = await ReadRequiredEntryAsync(
            entries,
            "constraints.md",
            errors,
            cancellationToken);

        var metadata = ParseManifest(manifestJson, errors);
        ValidateMetadata(metadata, errors);

        if (statement is not null && string.IsNullOrWhiteSpace(statement))
            errors.Add("statement.md cannot be empty.");

        if (constraints is not null && string.IsNullOrWhiteSpace(constraints))
            errors.Add("constraints.md cannot be empty.");

        var samples = await ReadSamplesAsync(entries, errors, cancellationToken);
        var judgeTestCases = await ReadJudgeTestCasesAsync(
            entries,
            errors,
            cancellationToken);

        var function = await ReadFunctionAsync(
            entries,
            metadata,
            samples,
            judgeTestCases,
            errors,
            cancellationToken);

        ThrowIfInvalid(errors);

        return new ProblemPackage(
            metadata!,
            statement!,
            constraints!,
            samples,
            judgeTestCases,
            function);
    }

    private Dictionary<string, ZipArchiveEntry> ValidateEntries(
        ZipArchive archive,
        ICollection<string> errors)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalUncompressedBytes = 0;
        var fileCount = 0;

        foreach (var entry in archive.Entries)
        {
            if (!TryValidateEntryName(entry.FullName, out var canonicalName, errors))
                continue;

            if (!names.Add(canonicalName))
            {
                errors.Add($"Duplicate archive entry name: {canonicalName}.");
                continue;
            }

            var isDirectory = canonicalName.EndsWith("/", StringComparison.Ordinal);
            if (isDirectory)
            {
                if (!AllowedDirectories.Contains(canonicalName))
                    errors.Add($"Unexpected archive directory: {canonicalName}.");
                continue;
            }

            fileCount++;
            if (entry.Length > _options.MaxEntryBytes)
            {
                errors.Add(
                    $"Archive entry {canonicalName} exceeds the " +
                    $"{_options.MaxEntryBytes}-byte entry limit.");
            }

            if (entry.Length > _options.MaxTotalUncompressedBytes - totalUncompressedBytes)
            {
                totalUncompressedBytes = _options.MaxTotalUncompressedBytes + 1;
            }
            else
            {
                totalUncompressedBytes += entry.Length;
            }

            if (!RequiredRootFiles.Contains(canonicalName) &&
                !FunctionFiles.Contains(canonicalName) &&
                !ContentEntryNamePattern().IsMatch(canonicalName))
            {
                errors.Add($"Unexpected archive entry: {canonicalName}.");
            }

            entries.Add(canonicalName, entry);
        }

        if (fileCount > _options.MaxFileCount)
        {
            errors.Add(
                $"The archive contains {fileCount} files; the limit is {_options.MaxFileCount}.");
        }

        if (totalUncompressedBytes > _options.MaxTotalUncompressedBytes)
        {
            errors.Add(
                "The archive exceeds the configured total uncompressed size limit.");
        }

        return entries;
    }

    private static bool TryValidateEntryName(
        string entryName,
        out string canonicalName,
        ICollection<string> errors)
    {
        canonicalName = entryName;
        if (string.IsNullOrWhiteSpace(entryName))
        {
            errors.Add("The archive contains an empty entry name.");
            return false;
        }

        if (entryName.Contains('\\'))
        {
            errors.Add("Archive entry names must use forward slashes.");
            return false;
        }

        var pathToCheck = entryName.EndsWith("/", StringComparison.Ordinal)
            ? entryName[..^1]
            : entryName;
        var segments = pathToCheck.Split('/');

        if (entryName.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(pathToCheck) ||
            segments.Any(segment =>
                segment.Length == 0 || segment is "." or ".."))
        {
            errors.Add("The archive contains an unsafe entry path.");
            return false;
        }

        return true;
    }

    private async Task<string?> ReadRequiredEntryAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string name,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!entries.TryGetValue(name, out var entry))
        {
            errors.Add($"Required archive entry is missing: {name}.");
            return null;
        }

        return await ReadTextEntryAsync(entry, name, errors, cancellationToken);
    }

    private static async Task<string?> ReadTextEntryAsync(
        ZipArchiveEntry entry,
        string name,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = entry.Open();
            using var reader = new StreamReader(
                stream,
                StrictUtf8,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 16 * 1024,
                leaveOpen: false);
            var content = await reader.ReadToEndAsync(cancellationToken);

            if (content.Contains('\0'))
            {
                errors.Add($"Archive entry {name} contains a null character.");
                return null;
            }

            return content;
        }
        catch (DecoderFallbackException)
        {
            errors.Add($"Archive entry {name} is not valid UTF-8.");
            return null;
        }
        catch (InvalidDataException)
        {
            errors.Add($"Archive entry {name} cannot be decompressed.");
            return null;
        }
        catch (IOException)
        {
            errors.Add($"Archive entry {name} could not be read.");
            return null;
        }
    }

    private static ProblemPackageMetadata? ParseManifest(
        string? manifestJson,
        ICollection<string> errors)
    {
        if (manifestJson is null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(manifestJson);
            ValidateDuplicateProperties(document.RootElement, "$", errors);

            if (document.RootElement.TryGetProperty("schemaVersion", out var schemaVersion) &&
                schemaVersion.TryGetInt32(out var schemaVersionValue))
            {
                var hasExecutionMode = document.RootElement.TryGetProperty(
                    "executionMode",
                    out _);
                if (schemaVersionValue == 1 && hasExecutionMode)
                    errors.Add("problem.json schema version 1 cannot declare executionMode.");
                if (schemaVersionValue == 2 && !hasExecutionMode)
                    errors.Add("problem.json schema version 2 requires executionMode.");
            }

            return JsonSerializer.Deserialize<ProblemPackageMetadata>(
                manifestJson,
                ManifestJsonOptions);
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber.HasValue
                ? $" near line {exception.LineNumber.Value + 1}"
                : string.Empty;
            errors.Add($"problem.json is invalid{line}.");
            return null;
        }
    }

    private static void ValidateDuplicateProperties(
        JsonElement element,
        string path,
        ICollection<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!properties.Add(property.Name))
                    errors.Add($"Duplicate JSON property {path}.{property.Name}.");

                ValidateDuplicateProperties(property.Value, $"{path}.{property.Name}", errors);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                ValidateDuplicateProperties(item, $"{path}[{index}]", errors);
                index++;
            }
        }
    }

    private void ValidateMetadata(
        ProblemPackageMetadata? metadata,
        ICollection<string> errors)
    {
        if (metadata is null)
            return;

        if (metadata.SchemaVersion is not (1 or 2))
            errors.Add("problem.json schemaVersion must be 1 or 2.");

        if (!Enum.IsDefined(metadata.ExecutionMode))
            errors.Add("Problem executionMode is invalid.");

        if (string.IsNullOrWhiteSpace(metadata.Slug) ||
            metadata.Slug.Length > 160 ||
            !SlugPattern().IsMatch(metadata.Slug))
        {
            errors.Add("Problem slug is invalid.");
        }

        if (string.IsNullOrWhiteSpace(metadata.Title) || metadata.Title.Length > 255)
            errors.Add("Problem title must contain 1-255 characters.");

        if (!Enum.IsDefined(metadata.Difficulty))
            errors.Add("Problem difficulty is invalid.");

        if (metadata.TimeLimitMs < _options.MinTimeLimitMs ||
            metadata.TimeLimitMs > _options.MaxTimeLimitMs)
        {
            errors.Add(
                $"timeLimitMs must be between {_options.MinTimeLimitMs} and " +
                $"{_options.MaxTimeLimitMs}.");
        }

        if (metadata.MemoryLimitKb < _options.MinMemoryLimitKb ||
            metadata.MemoryLimitKb > _options.MaxMemoryLimitKb)
        {
            errors.Add(
                $"memoryLimitKb must be between {_options.MinMemoryLimitKb} and " +
                $"{_options.MaxMemoryLimitKb}.");
        }

        if (metadata.Tags is null)
        {
            errors.Add("Problem tags cannot be null.");
            return;
        }

        if (metadata.Tags.Count > 10)
            errors.Add("A problem package can contain at most 10 tags.");

        var tagSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in metadata.Tags)
        {
            if (tag is null ||
                string.IsNullOrWhiteSpace(tag.Slug) ||
                tag.Slug.Length > 80 ||
                !SlugPattern().IsMatch(tag.Slug))
            {
                errors.Add("A tag slug is invalid.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(tag.Name) || tag.Name.Length > 100)
                errors.Add($"Tag {tag.Slug} must have a name of 1-100 characters.");

            if (!tagSlugs.Add(tag.Slug))
                errors.Add($"Duplicate tag slug: {tag.Slug}.");

            if (!string.IsNullOrWhiteSpace(tag.Name) && !tagNames.Add(tag.Name))
                errors.Add($"Duplicate tag name: {tag.Name}.");
        }
    }

    private async Task<IReadOnlyCollection<ProblemPackageSample>> ReadSamplesAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var groups = CollectCaseEntries(entries, "samples", allowExplanation: true, errors);
        if (groups.Count > _options.MaxSampleCount)
        {
            errors.Add(
                $"The package contains {groups.Count} samples; " +
                $"the limit is {_options.MaxSampleCount}.");
        }

        var samples = new List<ProblemPackageSample>();
        foreach (var (ordinal, parts) in groups.OrderBy(group => group.Key))
        {
            var input = await ReadCasePartAsync(
                parts,
                "in",
                "sample",
                ordinal,
                errors,
                cancellationToken);
            var output = await ReadCasePartAsync(
                parts,
                "out",
                "sample",
                ordinal,
                errors,
                cancellationToken);

            string? explanation = null;
            if (parts.TryGetValue("md", out var explanationEntry))
            {
                explanation = await ReadTextEntryAsync(
                    explanationEntry,
                    explanationEntry.FullName,
                    errors,
                    cancellationToken);
                if (string.IsNullOrWhiteSpace(explanation))
                    explanation = null;
            }

            if (input is not null && output is not null)
                samples.Add(new ProblemPackageSample(ordinal, input, output, explanation));
        }

        if (samples.Count == 0)
            errors.Add("The package must contain at least one complete public sample.");

        return samples;
    }

    private async Task<ProblemPackageFunction?> ReadFunctionAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ProblemPackageMetadata? metadata,
        IReadOnlyCollection<ProblemPackageSample> samples,
        IReadOnlyCollection<ProblemPackageJudgeTestCase> judgeTestCases,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var hasFunctionEntries = entries.Keys.Any(name =>
            name.StartsWith("function/", StringComparison.Ordinal) &&
            !name.EndsWith("/", StringComparison.Ordinal));
        if (metadata is null)
            return null;

        if (metadata.SchemaVersion == 1)
        {
            if (hasFunctionEntries)
                errors.Add("Schema-version-1 packages cannot contain function files.");
            return null;
        }

        if (metadata.ExecutionMode == ProblemExecutionMode.StdinStdout)
        {
            if (hasFunctionEntries)
                errors.Add("StdinStdout problems cannot contain function files.");
            return null;
        }

        if (metadata.ExecutionMode != ProblemExecutionMode.Function)
            return null;

        var signatureJson = await ReadRequiredEntryAsync(
            entries,
            "function/signature.json",
            errors,
            cancellationToken);
        var adapterTemplate = await ReadRequiredEntryAsync(
            entries,
            "function/adapter-template.cpp",
            errors,
            cancellationToken);
        if (signatureJson is not null &&
            Encoding.UTF8.GetByteCount(signatureJson) > _options.MaxFunctionSignatureBytes)
        {
            errors.Add(
                "function/signature.json exceeds the configured function-signature limit.");
        }
        if (adapterTemplate is not null &&
            Encoding.UTF8.GetByteCount(adapterTemplate) > _options.MaxFunctionAdapterBytes)
        {
            errors.Add(
                "function/adapter-template.cpp exceeds the configured function-adapter limit.");
        }
        var errorCountBeforeContractValidation = errors.Count;
        var signature = FunctionPackageValidator.ParseSignature(signatureJson, errors);
        FunctionPackageValidator.ValidateAdapterTemplate(adapterTemplate, errors);

        if (signature is not null && errors.Count == errorCountBeforeContractValidation)
        {
            foreach (var sample in samples)
            {
                FunctionPackageValidator.ValidateCase(
                    signature,
                    sample.Input,
                    sample.ExpectedOutput,
                    $"Sample {sample.Ordinal}",
                    errors);
            }

            foreach (var testCase in judgeTestCases)
            {
                FunctionPackageValidator.ValidateCase(
                    signature,
                    testCase.Input,
                    testCase.ExpectedOutput,
                    $"Judge test case {testCase.Ordinal}",
                    errors);
            }
        }

        return signature is null || signatureJson is null || adapterTemplate is null
            ? null
            : new ProblemPackageFunction(signature, signatureJson, adapterTemplate);
    }

    private async Task<IReadOnlyCollection<ProblemPackageJudgeTestCase>> ReadJudgeTestCasesAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        var groups = CollectCaseEntries(entries, "tests", allowExplanation: false, errors);
        if (groups.Count > _options.MaxJudgeTestCaseCount)
        {
            errors.Add(
                $"The package contains {groups.Count} judge test cases; " +
                $"the limit is {_options.MaxJudgeTestCaseCount}.");
        }

        var testCases = new List<ProblemPackageJudgeTestCase>();
        foreach (var (ordinal, parts) in groups.OrderBy(group => group.Key))
        {
            var input = await ReadCasePartAsync(
                parts,
                "in",
                "judge test case",
                ordinal,
                errors,
                cancellationToken);
            var output = await ReadCasePartAsync(
                parts,
                "out",
                "judge test case",
                ordinal,
                errors,
                cancellationToken);

            if (input is not null && output is not null)
            {
                testCases.Add(new ProblemPackageJudgeTestCase(ordinal, input, output));
            }
        }

        if (testCases.Count == 0)
            errors.Add("The package must contain at least one complete private judge test case.");

        return testCases;
    }

    private static SortedDictionary<int, Dictionary<string, ZipArchiveEntry>> CollectCaseEntries(
        IReadOnlyDictionary<string, ZipArchiveEntry> entries,
        string folder,
        bool allowExplanation,
        ICollection<string> errors)
    {
        var groups = new SortedDictionary<int, Dictionary<string, ZipArchiveEntry>>();
        var stems = new Dictionary<int, string>();

        foreach (var (name, entry) in entries)
        {
            var match = ContentEntryNamePattern().Match(name);
            if (!match.Success || !string.Equals(match.Groups[1].Value, folder, StringComparison.Ordinal))
                continue;

            var stem = match.Groups[2].Value;
            var extension = match.Groups[3].Value;
            var ordinal = int.Parse(stem, System.Globalization.CultureInfo.InvariantCulture);

            if (ordinal <= 0)
            {
                errors.Add($"Content ordinal must be positive: {name}.");
                continue;
            }

            if (extension == "md" && !allowExplanation)
            {
                errors.Add($"Judge test cases cannot contain explanations: {name}.");
                continue;
            }

            if (stems.TryGetValue(ordinal, out var existingStem) && existingStem != stem)
            {
                errors.Add(
                    $"Ordinal {ordinal} in {folder} is represented by multiple names.");
            }
            else
            {
                stems[ordinal] = stem;
            }

            if (!groups.TryGetValue(ordinal, out var parts))
            {
                parts = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
                groups.Add(ordinal, parts);
            }

            if (!parts.TryAdd(extension, entry))
                errors.Add($"Duplicate {extension} file for {folder} ordinal {ordinal}.");
        }

        return groups;
    }

    private static async Task<string?> ReadCasePartAsync(
        IReadOnlyDictionary<string, ZipArchiveEntry> parts,
        string extension,
        string contentKind,
        int ordinal,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        if (!parts.TryGetValue(extension, out var entry))
        {
            errors.Add($"The {contentKind} ordinal {ordinal} is missing its .{extension} file.");
            return null;
        }

        return await ReadTextEntryAsync(
            entry,
            entry.FullName,
            errors,
            cancellationToken);
    }

    private static void ThrowIfInvalid(ICollection<string> errors)
    {
        if (errors.Count > 0)
            throw new PackageValidationException(errors.Distinct(StringComparer.Ordinal));
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.NonBacktracking)]
    private static partial Regex SlugPattern();

    [GeneratedRegex(
        "^(samples|tests)/([0-9]{2,4})\\.(in|out|md)$",
        RegexOptions.NonBacktracking)]
    private static partial Regex ContentEntryNamePattern();
}
