using MediatR;

namespace RESQ.Application.UseCases.Finance.Commands.VerifyZaloPayPayment;

public record VerifyZaloPayPaymentCommand : IRequest<bool>
{
    /// <summary>
    /// The app_trans_id from the ZaloPay redirect query string.
    /// </summary>
    public string AppTransId { get; init; } = string.Empty;

    public string? SignedRedirectStatus { get; init; }

    public bool HasValidRedirectChecksum { get; init; }
}
