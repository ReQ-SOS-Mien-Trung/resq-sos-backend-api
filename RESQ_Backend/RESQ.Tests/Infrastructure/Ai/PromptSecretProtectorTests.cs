using Microsoft.Extensions.Options;
using RESQ.Infrastructure.Options;
using RESQ.Infrastructure.Services.Ai;

namespace RESQ.Tests.Infrastructure.Ai;

public class PromptSecretProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_ShouldRoundTrip_WhenMasterKeyIsConfigured()
    {
        var protector = CreateProtector("unit-test-master-key");

        var protectedValue = protector.Protect("super-secret-key");

        Assert.NotNull(protectedValue);
        Assert.NotEqual("super-secret-key", protectedValue);
        Assert.True(protector.IsProtected(protectedValue));
        Assert.Equal("super-secret-key", protector.Unprotect(protectedValue));
    }

    [Fact]
    public void Protect_ShouldThrow_WhenMasterKeyIsMissing()
    {
        var protector = CreateProtector(null);

        var exception = Assert.Throws<InvalidOperationException>(() => protector.Protect("super-secret-key"));

        Assert.Contains("master key", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unprotect_ShouldReturnPlaintext_WhenValueIsNotProtected()
    {
        var protector = CreateProtector(null);

        Assert.Equal("plain-text", protector.Unprotect("plain-text"));
    }

    private static PromptSecretProtector CreateProtector(string? masterKey)
    {
        return new PromptSecretProtector(Options.Create(new PromptSecretsOptions
        {
            MasterKey = masterKey
        }));
    }
}