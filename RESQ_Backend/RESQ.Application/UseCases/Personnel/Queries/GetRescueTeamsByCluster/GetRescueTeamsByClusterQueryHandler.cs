using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Extensions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Personnel;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Enum.Personnel;

namespace RESQ.Application.UseCases.Personnel.Queries.GetRescueTeamsByCluster;

public class GetRescueTeamsByClusterQueryHandler(
    ISosClusterRepository sosClusterRepository,
    IPersonnelQueryRepository personnelQueryRepository,
    IRescueTeamRadiusConfigRepository rescueTeamRadiusConfigRepository)
    : IRequestHandler<GetRescueTeamsByClusterQuery, List<RescueTeamByClusterDto>>
{
    private const double DefaultMaxRadiusKm = 10.0;

    public async Task<List<RescueTeamByClusterDto>> Handle(
        GetRescueTeamsByClusterQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Lấy cluster để có toạ độ trung tâm
        var cluster = await sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy SOS Cluster với ID = {request.ClusterId}.");

        double? clusterLat = cluster.CenterLatitude;
        double? clusterLon = cluster.CenterLongitude;

        // 2. Lấy bán kính tối đa từ cấu hình (mặc định 10 km)
        var radiusConfig = await rescueTeamRadiusConfigRepository.GetAsync(cancellationToken);
        var maxRadiusKm = radiusConfig?.MaxRadiusKm ?? DefaultMaxRadiusKm;

        // 3. Lấy tất cả đội cứu hộ đang ở trạng thái Available (sẵn sàng nhận nhiệm vụ)
        var teams = await personnelQueryRepository.GetAllAvailableTeamsAsync(cancellationToken);

        if (teams.Count == 0)
            return [];

        // 4. Build DTO + tính khoảng cách dựa trên location của AP đã được eager-load sẵn
        var dtos = teams.Select(team =>
        {
            double? distKm = null;

            if (clusterLat.HasValue && clusterLon.HasValue
                && team.AssemblyPointLocation is not null)
            {
                distKm = Math.Round(
                    HaversineKm(clusterLat.Value, clusterLon.Value,
                                team.AssemblyPointLocation.Latitude,
                                team.AssemblyPointLocation.Longitude),
                    2);
            }

            return new RescueTeamByClusterDto
            {
                Id = team.Id,
                Code = team.Code,
                Name = team.Name,
                TeamType = team.TeamType.ToString(),
                Status = team.Status.ToString(),
                AssemblyPointId = team.AssemblyPointId,
                AssemblyPointName = team.AssemblyPointName,
                DistanceKm = distKm,
                MaxMembers = team.MaxMembers,
                CurrentMemberCount = team.Members.Count(m => m.Status != TeamMemberStatus.Removed)
            };
        });

        // 5. Chỉ lấy các đội trong bán kính cho phép, sắp xếp theo khoảng cách
        return [.. dtos
            .Where(d => d.DistanceKm.HasValue && d.DistanceKm.Value <= maxRadiusKm)
            .OrderBy(d => d.DistanceKm)];
    }

    /// <summary>Haversine formula — returns straight-line distance in kilometres.</summary>
    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
