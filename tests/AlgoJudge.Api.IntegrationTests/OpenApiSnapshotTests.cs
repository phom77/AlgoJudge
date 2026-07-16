namespace AlgoJudge.Api.IntegrationTests;

public class OpenApiSnapshotTests
{
    [Fact]
    public void CanonicalizationIgnoresNonSemanticOrderingAndServerUrls()
    {
        const string first =
            """
            {
              "servers": [{ "url": "https://first.example" }],
              "required": ["beta", "alpha"],
              "schema": { "type": "object", "description": "stable" }
            }
            """;
        const string second =
            """
            {
              "schema": { "description": "stable", "type": "object" },
              "required": ["alpha", "beta"],
              "servers": [{ "url": "https://second.example" }]
            }
            """;

        Assert.Equal(
            OpenApiSnapshot.Canonicalize(first),
            OpenApiSnapshot.Canonicalize(second));
    }

    [Fact]
    public void CanonicalizationPreservesSemanticChanges()
    {
        const string stringSchema =
            """{ "schema": { "type": "string" } }""";
        const string integerSchema =
            """{ "schema": { "type": "integer" } }""";

        Assert.NotEqual(
            OpenApiSnapshot.Canonicalize(stringSchema),
            OpenApiSnapshot.Canonicalize(integerSchema));
    }

    [Fact]
    public void CanonicalizationPreservesOrderedArrayChanges()
    {
        const string stringThenInteger =
            """{ "prefixItems": [{ "type": "string" }, { "type": "integer" }] }""";
        const string integerThenString =
            """{ "prefixItems": [{ "type": "integer" }, { "type": "string" }] }""";

        Assert.NotEqual(
            OpenApiSnapshot.Canonicalize(stringThenInteger),
            OpenApiSnapshot.Canonicalize(integerThenString));
    }
}
