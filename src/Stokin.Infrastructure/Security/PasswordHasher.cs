using System.Security.Cryptography;
namespace Stokin.Infrastructure.Security;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        if (password.Length < 10 || password.Length > 128) throw new ArgumentException("Kata sandi harus 10 sampai 128 karakter.");
        var salt = RandomNumberGenerator.GetBytes(16); var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 600000, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2-SHA256$600000${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }
    public static bool Verify(string password, string hash)
    {
        try { var p = hash.Split('$'); if (p.Length != 4 || p[0] != "PBKDF2-SHA256") return false; int rounds = int.Parse(p[1]); if (rounds < 100000 || rounds > 2000000) return false; var expected = Convert.FromBase64String(p[3]); var actual = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(p[2]), rounds, HashAlgorithmName.SHA256, expected.Length); return CryptographicOperations.FixedTimeEquals(actual, expected); } catch { return false; }
    }
}
