using RESQ.Application.Common.Security;

namespace RESQ.Tests.Application.Common.Security;

public class SecretMaskerTests
{
    [Fact]
    public void Mask_ShouldReturnNull_WhenSecretIsMissing()
    {
        SecretMasker.Mask(null).ShouldBeNull();
        SecretMasker.Mask(string.Empty).ShouldBeNull();
        SecretMasker.Mask("   ").ShouldBeNull();
    }

    [Fact]
    public void Mask_ShouldHideMiddleCharacters_ForLongSecrets()
    {
        var masked = SecretMasker.Mask("abcdefgh12345678");

        Assert.Equal("abcd...5678", masked);
    }

    [Fact]
    public void Mask_ShouldFullyMaskShortSecrets()
    {
        var masked = SecretMasker.Mask("abcd123");

        Assert.Equal("*******", masked);
    }
}

internal static class AssertionExtensions
{
    public static void ShouldBeNull(this object? value)
    {
        Assert.Null(value);
    }
}
