namespace RESQ.Domain.Entities.System;

public class ServiceZoneModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Danh sách tọa độ đỉnh polygon, theo thứ tự. Polygon sẽ tự động đóng lại.
    /// </summary>
    public List<CoordinatePoint> Coordinates { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public Guid? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CoordinatePoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
