using MediatR;

namespace RESQ.Application.UseCases.Emergency.Queries.GetRescueTeamsByCluster;

/// <summary>
/// Lấy danh sách rescue team đang Available và có điểm tập kết,
/// sắp xếp tăng dần theo khoảng cách từ điểm tập kết tới tâm cluster SOS.
/// </summary>
public record GetRescueTeamsByClusterQuery(int ClusterId)
    : IRequest<List<ClusterRescueTeamDto>>;
