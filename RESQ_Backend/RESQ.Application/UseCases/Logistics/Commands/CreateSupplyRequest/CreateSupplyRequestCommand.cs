using MediatR;

namespace RESQ.Application.UseCases.Logistics.Commands.CreateSupplyRequest;

public class CreateSupplyRequestCommand : IRequest<CreateSupplyRequestResponse>
{
    public Guid RequestingUserId { get; set; }
    public int? DepotId { get; set; }
    public List<SupplyRequestGroupDto> Requests { get; set; } = new();
}
