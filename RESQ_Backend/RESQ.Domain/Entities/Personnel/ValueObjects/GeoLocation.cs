using RESQ.Domain.Entities.Personnel.Exceptions;

namespace RESQ.Domain.Entities.Personnel.ValueObjects;

public sealed class GeoLocation
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new InvalidGeoLocationException($"Vĩ độ (Latitude) '{latitude}' không hợp lệ. Phải từ -90 đến 90.");

        if (longitude < -180 || longitude > 180)
            throw new InvalidGeoLocationException($"Kinh độ (Longitude) '{longitude}' không hợp lệ. Phải từ -180 đến 180.");

        Latitude = latitude;
        Longitude = longitude;
    }
}