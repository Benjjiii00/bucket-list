namespace BucketListAPI.Models;

public class Trail
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? BikeparkId { get; set; }

    public Bikepark? Bikepark { get; set; }

    public double Length { get; set; }

    public int ElevationGain { get; set; }

    public string? Difficulty { get; set; }

    public string? TrailType { get; set; }

    public string? Description { get; set; }

    public string? Polyline { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
