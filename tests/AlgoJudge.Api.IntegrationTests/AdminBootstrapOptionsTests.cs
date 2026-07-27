using AlgoJudge.API.Security;

namespace AlgoJudge.Api.IntegrationTests;

public sealed class AdminBootstrapOptionsTests
{
    [Fact]
    public void NormalizesConfiguredEmailsForCaseInsensitivePromotion()
    {
        var options = new AdminBootstrapOptions
        {
            Emails = ["  Admin@Example.Test ", "admin@example.test"]
        };

        var emails = options.ParseNormalizedEmails();

        Assert.Single(emails);
        Assert.Contains("ADMIN@EXAMPLE.TEST", emails);
    }

    [Fact]
    public void RejectsInvalidBootstrapEmail()
    {
        var options = new AdminBootstrapOptions { Emails = ["not-an-email"] };

        Assert.Throws<InvalidOperationException>(options.ParseNormalizedEmails);
    }
}
