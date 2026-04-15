using MediatR;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPoints;

public class GetAllAssemblyPointsQuery : IRequest<GetAllAssemblyPointsResponse>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    /// <summary>Lọc theo trạng thái. Null = lấy tất cả.</summary>
    public AssemblyPointStatus? Status { get; set; }
}
