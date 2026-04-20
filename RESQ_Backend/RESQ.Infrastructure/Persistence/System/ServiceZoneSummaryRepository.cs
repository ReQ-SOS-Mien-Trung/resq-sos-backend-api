using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using RESQ.Application.Repositories.Base;
using RESQ.Application.Repositories.System;
using RESQ.Domain.Entities.System;
using RESQ.Domain.Enum.Emergency;
using RESQ.Infrastructure.Entities.Emergency;
using RESQ.Infrastructure.Entities.Logistics;
using RESQ.Infrastructure.Entities.Operations;
using RESQ.Infrastructure.Entities.Personnel;

namespace RESQ.Infrastructure.Persistence.System;

public class ServiceZoneSummaryRepository(IUnitOfWork unitOfWork) : IServiceZoneSummaryRepository
{
    private const int Srid = 4326;
    private static readonly string PendingStatus = SosRequestStatus.Pending.ToString();
    private static readonly string IncidentStatus = SosRequestStatus.Incident.ToString();
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), Srid);

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IReadOnlyDictionary<int, ServiceZoneResourceCounts>> GetResourceCountsAsync(
        IReadOnlyCollection<ServiceZoneModel> serviceZones,
        CancellationToken cancellationToken = default)
    {
        if (serviceZones.Count == 0)
        {
            return new Dictionary<int, ServiceZoneResourceCounts>();
        }

        var result = new Dictionary<int, ServiceZoneResourceCounts>();

        foreach (var zone in serviceZones)
        {
            var polygon = CreatePolygon(zone);
            if (polygon is null)
            {
                result[zone.Id] = EmptyCounts(zone.Id);
                continue;
            }

            var sosCounts = await CountSosRequestsAsync(polygon, cancellationToken);
            var teamIncidentCount = await CountTeamIncidentsAsync(polygon, cancellationToken);
            var assemblyPointCount = await CountAssemblyPointsAsync(polygon, cancellationToken);
            var depotCount = await CountDepotsAsync(polygon, cancellationToken);

            result[zone.Id] = new ServiceZoneResourceCounts(
                zone.Id,
                sosCounts.Pending,
                sosCounts.Incident,
                teamIncidentCount,
                assemblyPointCount,
                depotCount);
        }

        return result;
    }

    private async Task<(int Pending, int Incident)> CountSosRequestsAsync(
        Polygon polygon,
        CancellationToken cancellationToken)
    {
        var envelope = polygon.EnvelopeInternal;
        var candidates = await _unitOfWork.Set<SosRequest>()
            .Where(x => x.Location != null
                && (x.Status == PendingStatus || x.Status == IncidentStatus))
            .Select(x => new
            {
                x.Status,
                x.Location
            })
            .ToListAsync(cancellationToken);

        var pending = 0;
        var incident = 0;

        foreach (var candidate in candidates.Where(candidate =>
            IsInsideEnvelope(envelope, candidate.Location) && Covers(polygon, candidate.Location)))
        {
            if (candidate.Status == PendingStatus)
            {
                pending++;
            }
            else if (candidate.Status == IncidentStatus)
            {
                incident++;
            }
        }

        return (pending, incident);
    }

    private async Task<int> CountTeamIncidentsAsync(Polygon polygon, CancellationToken cancellationToken)
    {
        var locations = await LoadTeamIncidentLocationsAsync(cancellationToken);
        return CountLocationsInsidePolygon(polygon, locations);
    }

    private async Task<int> CountAssemblyPointsAsync(Polygon polygon, CancellationToken cancellationToken)
    {
        var locations = await LoadAssemblyPointLocationsAsync(cancellationToken);
        return CountLocationsInsidePolygon(polygon, locations);
    }

    private async Task<int> CountDepotsAsync(Polygon polygon, CancellationToken cancellationToken)
    {
        var locations = await LoadDepotLocationsAsync(cancellationToken);
        return CountLocationsInsidePolygon(polygon, locations);
    }

    private Task<List<Point?>> LoadTeamIncidentLocationsAsync(CancellationToken cancellationToken) =>
        _unitOfWork.Set<TeamIncident>()
            .Where(x => x.Location != null)
            .Select(x => x.Location)
            .ToListAsync(cancellationToken);

    private Task<List<Point?>> LoadAssemblyPointLocationsAsync(CancellationToken cancellationToken) =>
        _unitOfWork.Set<AssemblyPoint>()
            .Where(x => x.Location != null)
            .Select(x => x.Location)
            .ToListAsync(cancellationToken);

    private Task<List<Point?>> LoadDepotLocationsAsync(CancellationToken cancellationToken) =>
        _unitOfWork.Set<Depot>()
            .Where(x => x.Location != null)
            .Select(x => x.Location)
            .ToListAsync(cancellationToken);

    private static int CountLocationsInsidePolygon(Polygon polygon, IEnumerable<Point?> locations)
    {
        var envelope = polygon.EnvelopeInternal;

        return locations.Count(location =>
            IsInsideEnvelope(envelope, location) && Covers(polygon, location));
    }

    private static bool IsInsideEnvelope(Envelope envelope, Point? point) =>
        point is not null
        && point.Y >= envelope.MinY
        && point.Y <= envelope.MaxY
        && point.X >= envelope.MinX
        && point.X <= envelope.MaxX;

    private static Polygon? CreatePolygon(ServiceZoneModel zone)
    {
        if (zone.Coordinates.Count < 3)
        {
            return null;
        }

        try
        {
            var coordinates = zone.Coordinates
                .Select(point => new Coordinate(point.Longitude, point.Latitude))
                .ToList();

            if (!coordinates[0].Equals2D(coordinates[^1]))
            {
                coordinates.Add(coordinates[0]);
            }

            if (coordinates.Count < 4)
            {
                return null;
            }

            var ring = GeometryFactory.CreateLinearRing(coordinates.ToArray());
            var polygon = GeometryFactory.CreatePolygon(ring);

            return polygon.IsValid ? polygon : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool Covers(Polygon polygon, Point? point)
    {
        if (point is null)
        {
            return false;
        }

        try
        {
            return polygon.Covers(point);
        }
        catch
        {
            return false;
        }
    }

    private static ServiceZoneResourceCounts EmptyCounts(int serviceZoneId) =>
        new(serviceZoneId, 0, 0, 0, 0, 0);
}
