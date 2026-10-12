using System.Security.Cryptography;
using System.Text;

namespace Turniejo.Users.Application.Users.RequestPasswordReset;

public static class PasswordResetTokenGenerator
{
    public static (string Token, string Hash) Generate()
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return (token, Hash(token));
    }

    // Token ma 256 bitów losowości, więc zwykły SHA-256 wystarcza — nie ma czego łamać słownikiem.
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
