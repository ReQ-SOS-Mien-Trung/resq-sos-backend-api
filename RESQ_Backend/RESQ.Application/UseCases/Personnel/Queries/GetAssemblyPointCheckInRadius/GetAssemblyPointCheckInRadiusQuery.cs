using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.GetAssemblyPointCheckInRadius;

/// <summary>Lấy cấu hình bán kính check-in cho điểm tập kết. Nếu chưa có cấu hình riêng sẽ trả về cấu hình toàn cục.</summary>
public record GetAssemblyPointCheckInRadiusQuery(int AssemblyPointId) : IRequest<GetAssemblyPointCheckInRadiusResponse>;
