using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAllAssemblyPointCheckInRadiusConfigs;

/// <summary>Lấy toàn bộ cấu hình bán kính check-in riêng đang được thiết lập theo từng điểm tập kết.</summary>
public record GetAllAssemblyPointCheckInRadiusConfigsQuery : IRequest<GetAllAssemblyPointCheckInRadiusConfigsResponse>;
