using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotManagers;

public class DepotManagerInfoDto
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? AssignedAt { get; set; }
}

/// <summary>Lấy danh sách manager đang active (UnassignedAt IS NULL) trong một kho cụ thể.</summary>
public record GetDepotManagersQuery(int DepotId) : IRequest<List<DepotManagerInfoDto>>;
