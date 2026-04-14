using RESQ.Domain.Entities.Logistics.Exceptions;

namespace RESQ.Domain.Entities.Logistics.ValueObjects;

public sealed class GeoLocation
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new InvalidGeoLocationException($"Vi d? (Latitude) '{latitude}' không h?p l?. Ph?i t? -90 d?n 90.");

        if (longitude < -180 || longitude > 180)
            throw new InvalidGeoLocationException($"Kinh d? (Longitude) '{longitude}' không h?p l?. Ph?i t? -180 d?n 180.");

        Latitude = latitude;
        Longitude = longitude;
    }
}
