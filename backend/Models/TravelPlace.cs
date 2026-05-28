namespace BucketListAPI.Models;

public class TravelPlace
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Country { get; set; }

    public string? Region { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public PlaceStatus Status { get; set; } = PlaceStatus.BucketList;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}