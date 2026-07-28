using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlgoJudge.Application.Contracts.Admin;
using AlgoJudge.Domain.Enums;

namespace AlgoJudge.ContentTool.Workspace;

internal sealed class ContentBatchApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        WorkspaceJson.SerializerOptions);

    private readonly Uri _baseUri;
    private readonly string _accessToken;

    public ContentBatchApiClient(string baseUrl, string accessToken)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "The content batch API base URL must be an absolute HTTP(S) URL.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        _baseUri = baseUri;
        _accessToken = accessToken;
    }

    public async Task<ContentBatchResponse> CreateAndStartAsync(
        string catalogPath,
        ContentWorkspaceResolution resolution,
        CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = _baseUri };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _accessToken);
        var request = new CreateContentBatchRequest
        {
            CatalogName = Path.GetFileName(catalogPath),
            Items = resolution.Problems.Select(Map).ToArray()
        };
        using var createResponse = await client.PostAsJsonAsync(
            "/api/internal/admin/content-batches",
            request,
            JsonOptions,
            cancellationToken);
        var created = await ReadRequiredAsync(createResponse, cancellationToken);
        using var startResponse = await client.PostAsync(
            $"/api/internal/admin/content-batches/{created.Id}/start",
            content: null,
            cancellationToken);
        return await ReadRequiredAsync(startResponse, cancellationToken);
    }

    private static CreateContentBatchItemRequest Map(ResolvedWorkspaceProblem problem) => new()
    {
        CatalogPath = problem.CatalogPath,
        Action = problem.Action switch
        {
            "create" => ContentBatchImportAction.Create,
            "update-draft" => ContentBatchImportAction.UpdateDraft,
            "new-revision" => ContentBatchImportAction.NewRevision,
            "skip" => ContentBatchImportAction.Skip,
            _ => throw new InvalidOperationException("Resolved catalog action is invalid.")
        },
        ContentHash = problem.ContentHash,
        Slug = problem.Metadata.Slug,
        Title = problem.Metadata.Title,
        StatementMarkdown = problem.Metadata.Statement,
        ConstraintsMarkdown = string.Join('\n', problem.Metadata.Constraints),
        Difficulty = problem.Metadata.Difficulty,
        Tags = problem.Metadata.Tags,
        TimeLimitMs = problem.Metadata.TimeLimitMs,
        MemoryLimitKb = problem.Metadata.MemoryLimitKb,
        Samples = problem.Metadata.Samples.Select(sample => new ProblemSampleRequest
        {
            Input = sample.Arguments.GetRawText(),
            ExpectedOutput = sample.Expected.GetRawText(),
            Explanation = sample.Explanation
        }).ToArray(),
        GeneratorParameters = problem.GeneratorParameters.Clone(),
        Definition = problem.Definition
    };

    private static async Task<ContentBatchResponse> ReadRequiredAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadSafeErrorAsync(response, cancellationToken);
            throw new InvalidOperationException(
                $"Content batch API returned {(int)response.StatusCode}: {detail}");
        }
        return await response.Content.ReadFromJsonAsync<ContentBatchResponse>(
                   JsonOptions,
                   cancellationToken)
               ?? throw new InvalidOperationException(
                   "Content batch API returned an empty response.");
    }

    private static async Task<string> ReadSafeErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("detail", out var detail) &&
                   detail.ValueKind == JsonValueKind.String
                ? Bound(detail.GetString(), 512)
                : "The request failed.";
        }
        catch (JsonException)
        {
            return "The request failed.";
        }
    }

    private static string Bound(string? value, int maximum) =>
        string.IsNullOrWhiteSpace(value)
            ? "The request failed."
            : value[..Math.Min(value.Length, maximum)];
}
