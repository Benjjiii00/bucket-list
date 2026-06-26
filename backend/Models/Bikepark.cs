namespace BucketListAPI.Models;

public class Bikepark
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Country { get; set; }

    public string? Region { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string? Description { get; set; }

    public string? Difficulty { get; set; }

    public string? Website { get; set; }

    public string? PhotoUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<Trail> Trails { get; set; } = [];
}
