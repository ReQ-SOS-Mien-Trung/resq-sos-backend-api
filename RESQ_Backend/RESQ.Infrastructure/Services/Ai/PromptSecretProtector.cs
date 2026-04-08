using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RESQ.Application.Services.Ai;
using RESQ.Infrastructure.Options;

namespace RESQ.Infrastructure.Services.Ai;

public class PromptSecretProtector : IPromptSecretProtector
{
    private const string Prefix = "enc:v1:";
    private readonly byte[]? _key;

    public PromptSecretProtector(IOptions<PromptSecretsOptions> options)
    {
        var masterKey = options.Value.MasterKey;
        if (!string.IsNullOrWhiteSpace(masterKey))
        {
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(masterKey));
        }
    }

    public bool HasActiveKey => _key != null;

    public bool IsProtected(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.StartsWith(Prefix, StringComparison.Ordinal);
    }

    public string? Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (IsProtected(value))
        {
            return value;
        }

        if (_key == null)
        {
            throw new InvalidOperationException("Prompt secret master key is not configured.");
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!IsProtected(value))
        {
            return value;
        }

        if (_key == null)
        {
            throw new InvalidOperationException("Prompt secret master key is not configured.");
        }

        try
        {
            var payload = Convert.FromBase64String(value[Prefix.Length..]);
            var nonce = payload[..12];
            var tag = payload[12..28];
            var ciphertext = payload[28..];
            var plaintext = new byte[ciphertext.Length];

            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("Prompt secret could not be decrypted with the configured master key.", ex);
        }
    }
}