using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.RejectSupplyRequest;

public class RejectSupplyRequestCommand : IRequest<RejectSupplyRequestResponse>
{
    public int SupplyRequestId { get; set; }
    public Guid UserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
