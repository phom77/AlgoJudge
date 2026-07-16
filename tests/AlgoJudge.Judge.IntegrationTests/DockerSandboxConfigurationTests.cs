using AlgoJudge.Infrastructure.Grading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlgoJudge.Judge.IntegrationTests;

public sealed class DockerSandboxConfigurationTests
{
    [Theory]
    [InlineData("gcc:14")]
    [InlineData("algojudge/judge-cpp17:dev")]
    [InlineData("algojudge/judge-cpp17:latest")]
    public void FloatingOrDevelopmentImageIsRejected(string image)
    {
        var configuration = CreateConfiguration(image);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DockerSandboxService(
                configuration,
                NullLogger<DockerSandboxService>.Instance));

        Assert.Contains("must not use", exception.Message);
    }

    [Fact]
    public void VersionedJudgeImageIsAccepted()
    {
        var configuration = CreateConfiguration("algojudge/judge-cpp17:14.3.0-v1");

        var sandbox = new DockerSandboxService(
            configuration,
            NullLogger<DockerSandboxService>.Instance);

        Assert.NotNull(sandbox);
    }

    private static IConfiguration CreateConfiguration(string image)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sandbox:DockerImage"] = image
            })
            .Build();
    }
}
