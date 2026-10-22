using Stokin.Testing;
using Stokin.Infrastructure.Security;
namespace Stokin.IntegrationTests;

public class SecurityTests
{
    [Test]
    public void Passwords_use_unique_salts_and_reject_incorrect_password()
    {
        var a = PasswordHasher.Hash("KataSandi123!"); var b = PasswordHasher.Hash("KataSandi123!"); Assert.True(a != b); Assert.True(!a.Contains("KataSandi")); Assert.True(PasswordHasher.Verify("KataSandi123!", a)); Assert.True(!PasswordHasher.Verify("salah", a));
    }
}
