using BucketListAPI.Models;
using BucketListAPI.ORM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BucketListAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BikeparksController : ControllerBase
{
    private readonly BucketListDbContext _dbContext;

    public BikeparksController(BucketListDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<Bikepark>>> GetAllAsync()
    {
        var bikeparks = await _dbContext.Bikeparks
            .AsNoTracking()
            .Include(park => park.Trails)
            .OrderBy(park => park.Name)
            .ToListAsync();

        return Ok(bikeparks);
    }

    [HttpGet("{id:int}", Name = "GetBikeparkById")]
    public async Task<ActionResult<Bikepark>> GetByIdAsync(int id)
    {
        var bikepark = await _dbContext.Bikeparks
            .Include(park => park.Trails)
            .FirstOrDefaultAsync(park => park.Id == id);

        return bikepark is null ? NotFound() : Ok(bikepark);
    }

    [HttpPost]
    public async Task<ActionResult<Bikepark>> CreateAsync([FromBody] Bikepark bikepark)
    {
        if (!IsValidBikepark(bikepark, out var validationError))
        {
            return BadRequest(validationError);
        }

        bikepark.Id = 0;
        bikepark.CreatedAtUtc = DateTime.UtcNow;
        bikepark.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.Bikeparks.Add(bikepark);
        await _dbContext.SaveChangesAsync();

        return CreatedAtRoute("GetBikeparkById", new { id = bikepark.Id }, bikepark);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Bikepark>> UpdateAsync(int id, [FromBody] Bikepark bikepark)
    {
        if (!IsValidBikepark(bikepark, out var validationError))
        {
            return BadRequest(validationError);
        }

        var existingPark = await _dbContext.Bikeparks.FindAsync(id);
        if (existingPark is null)
        {
            return NotFound();
        }

        existingPark.Name = bikepark.Name.Trim();
        existingPark.Country = NormalizeOptionalText(bikepark.Country);
        existingPark.Region = NormalizeOptionalText(bikepark.Region);
        existingPark.Latitude = bikepark.Latitude;
        existingPark.Longitude = bikepark.Longitude;
        existingPark.Description = NormalizeOptionalText(bikepark.Description);
        existingPark.Difficulty = NormalizeOptionalText(bikepark.Difficulty);
        existingPark.Website = NormalizeOptionalText(bikepark.Website);
        existingPark.PhotoUrl = NormalizeOptionalText(bikepark.PhotoUrl);
        existingPark.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(existingPark);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var bikepark = await _dbContext.Bikeparks.FindAsync(id);
        if (bikepark is null)
        {
            return NotFound();
        }

        _dbContext.Bikeparks.Remove(bikepark);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsValidBikepark(Bikepark bikepark, out string? validationError)
    {
        if (bikepark is null)
        {
            validationError = "Der Bikepark darf nicht leer sein.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bikepark.Name))
        {
            validationError = "Der Name ist erforderlich.";
            return false;
        }

        if (bikepark.Latitude is < -90 or > 90)
        {
            validationError = "Der Breitengrad liegt außerhalb des gültigen Bereichs.";
            return false;
        }

        if (bikepark.Longitude is < -180 or > 180)
        {
            validationError = "Der Längengrad liegt außerhalb des gültigen Bereichs.";
            return false;
        }

        validationError = null;
        return true;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
