using System.Security.Cryptography;
using System.Text;

namespace Api.Auth;

public sealed class GmCredentials(IConfiguration configuration)
{
    private readonly string? _username = configuration["Admin:Username"];
    private readonly string? _password = configuration["Admin:Password"];

    public bool IsConfigured => !string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password);

    public string? Username => _username;

    public bool Verify(string? username, string? password)
    {
        if (!IsConfigured || username is null || password is null)
        {
            return false;
        }

        // Check both values every time so the response time doesn't reveal which one was wrong.
        var usernameMatches = FixedTimeEquals(username, _username!);
        var passwordMatches = FixedTimeEquals(password, _password!);
        return usernameMatches && passwordMatches;
    }

    // Hashing first gives equal-length inputs, so the comparison doesn't reveal the secret's length.
    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(left)),
            SHA256.HashData(Encoding.UTF8.GetBytes(right)));
}
