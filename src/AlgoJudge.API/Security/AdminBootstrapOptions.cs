using System.Net.Mail;

namespace AlgoJudge.API.Security;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    public IReadOnlyList<string> Emails { get; init; } = [];

    public IReadOnlySet<string> ParseNormalizedEmails()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in Emails)
        {
            var email = value.Trim();
            if (email.Length == 0 || !MailAddress.TryCreate(email, out _))
            {
                throw new InvalidOperationException(
                    "AdminBootstrap:Emails must contain valid non-empty email addresses.");
            }

            result.Add(email.ToUpperInvariant());
        }

        return result;
    }
}
