using MediatR;
using RESQ.Application.Common.Models.Finance.Momo;

namespace RESQ.Application.UseCases.Finance.Commands.ProcessMomoPayment;

public record ProcessMomoPaymentCommand : IRequest<bool>
{
    public MomoIpnRequest IpnData { get; init; } = null!;
}
