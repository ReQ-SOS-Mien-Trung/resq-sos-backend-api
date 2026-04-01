using MediatR;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamsByCluster;

/// <summary>Lấy danh sách đội cứu hộ sắp xếp theo khoảng cách gần nhất so với vị trí trung tâm của cluster SOS.</summary>
public record GetRescueTeamsByClusterQuery(int ClusterId) : IRequest<List<RescueTeamByClusterDto>>;
