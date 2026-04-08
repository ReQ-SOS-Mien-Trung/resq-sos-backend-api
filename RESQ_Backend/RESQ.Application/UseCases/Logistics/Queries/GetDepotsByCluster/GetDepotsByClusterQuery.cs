using MediatR;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotsByCluster;

/// <summary>
/// Lấy danh sách kho đang Available (còn hàng), sắp xếp tăng dần theo khoảng cách
/// từ vị trí kho tới tâm cluster SOS. Sử dụng chung bán kính cấu hình với đội cứu hộ.
/// </summary>
public record GetDepotsByClusterQuery(int ClusterId)
    : IRequest<List<DepotByClusterDto>>;
