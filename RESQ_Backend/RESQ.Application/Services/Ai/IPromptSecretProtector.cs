namespace RESQ.Application.Services.Ai;

public interface IPromptSecretProtector
{
    bool HasActiveKey { get; }

    bool IsProtected(string? value);

    string? Protect(string? value);

    string? Unprotect(string? value);
}