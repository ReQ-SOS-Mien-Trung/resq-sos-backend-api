using MediatR;

namespace RESQ.Application.UseCases.Personnel.Commands.DeleteAssemblyPointCheckInRadius;

/// <summary>Xóa cấu hình bán kính check-in riêng của điểm tập kết; điểm sẽ quay về dùng cấu hình toàn cục.</summary>
public record DeleteAssemblyPointCheckInRadiusCommand(int AssemblyPointId) : IRequest;
