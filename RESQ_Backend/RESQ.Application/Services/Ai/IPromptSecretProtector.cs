namespace RESQ.Application.Services.Ai;

public interface IAiSecretProtector
{
    bool HasActiveKey { get; }

    bool IsProtected(string? value);

    string? Protect(string? value);

    string? Unprotect(string? value);
}

public interface IPromptSecretProtector : IAiSecretProtector
{
}
