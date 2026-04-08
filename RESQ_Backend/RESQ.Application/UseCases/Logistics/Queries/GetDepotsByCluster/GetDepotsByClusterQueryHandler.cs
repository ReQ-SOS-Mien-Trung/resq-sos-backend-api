using MediatR;
using RESQ.Application.Exceptions;
using RESQ.Application.Repositories.Emergency;
using RESQ.Application.Repositories.Logistics;
using RESQ.Application.Repositories.System;

namespace RESQ.Application.UseCases.Logistics.Queries.GetDepotsByCluster;

public class GetDepotsByClusterQueryHandler(
    ISosClusterRepository sosClusterRepository,
    IDepotRepository depotRepository,
    IRescueTeamRadiusConfigRepository rescueTeamRadiusConfigRepository)
    : IRequestHandler<GetDepotsByClusterQuery, List<DepotByClusterDto>>
{
    private const double DefaultMaxRadiusKm = 10.0;

    public async Task<List<DepotByClusterDto>> Handle(
        GetDepotsByClusterQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Lấy cluster để có toạ độ trung tâm
        var cluster = await sosClusterRepository.GetByIdAsync(request.ClusterId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy SOS Cluster với ID = {request.ClusterId}.");

        double? clusterLat = cluster.CenterLatitude;
        double? clusterLon = cluster.CenterLongitude;

        // 2. Lấy bán kính tối đa từ cấu hình dùng chung với đội cứu hộ (mặc định 10 km)
        var radiusConfig = await rescueTeamRadiusConfigRepository.GetAsync(cancellationToken);
        var maxRadiusKm = radiusConfig?.MaxRadiusKm ?? DefaultMaxRadiusKm;

        // 3. Lấy tất cả kho đang hoạt động (Available) và còn hàng
        var depots = await depotRepository.GetAvailableDepotsAsync(cancellationToken);

        var depotList = depots.ToList();
        if (depotList.Count == 0)
            return [];

        // 4. Build DTO + tính khoảng cách dựa trên vị trí kho
        var dtos = depotList.Select(depot =>
        {
            double? distKm = null;

            if (clusterLat.HasValue && clusterLon.HasValue && depot.Location is not null)
            {
                distKm = Math.Round(
                    HaversineKm(clusterLat.Value, clusterLon.Value,
                                depot.Location.Latitude,
                                depot.Location.Longitude),
                    2);
            }

            return new DepotByClusterDto
            {
                Id = depot.Id,
                Name = depot.Name,
                Address = depot.Address,
                Status = depot.Status.ToString(),
                Capacity = depot.Capacity,
                CurrentUtilization = depot.CurrentUtilization,
                Latitude = depot.Location?.Latitude,
                Longitude = depot.Location?.Longitude,
                DistanceKm = distKm
            };
        });

        // 5. Chỉ lấy các kho trong bán kính cho phép, sắp xếp theo khoảng cách
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
