using BucketListAPI.Models;
using BucketListAPI.ORM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BucketListAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TrailsController : ControllerBase
{
    private readonly BucketListDbContext _dbContext;

    public TrailsController(BucketListDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<Trail>>> GetAllAsync()
    {
        var trails = await _dbContext.Trails
            .AsNoTracking()
            .OrderBy(trail => trail.Name)
            .ToListAsync();

        return Ok(trails);
    }

    [HttpGet("{id:int}", Name = "GetTrailById")]
    public async Task<ActionResult<Trail>> GetByIdAsync(int id)
    {
        var trail = await _dbContext.Trails.FindAsync(id);
        return trail is null ? NotFound() : Ok(trail);
    }

    [HttpPost]
    public async Task<ActionResult<Trail>> CreateAsync([FromBody] Trail trail)
    {
        if (!IsValidTrail(trail, out var validationError))
        {
            return BadRequest(validationError);
        }

        trail.Id = 0;
        trail.CreatedAtUtc = DateTime.UtcNow;
        trail.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.Trails.Add(trail);
        await _dbContext.SaveChangesAsync();

        return CreatedAtRoute("GetTrailById", new { id = trail.Id }, trail);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<Trail>> UpdateAsync(int id, [FromBody] Trail trail)
    {
        if (!IsValidTrail(trail, out var validationError))
        {
            return BadRequest(validationError);
        }

        var existingTrail = await _dbContext.Trails.FindAsync(id);
        if (existingTrail is null)
        {
            return NotFound();
        }

        existingTrail.Name = trail.Name.Trim();
        existingTrail.BikeparkId = trail.BikeparkId;
        existingTrail.Length = trail.Length;
        existingTrail.ElevationGain = trail.ElevationGain;
        existingTrail.Difficulty = NormalizeOptionalText(trail.Difficulty);
        existingTrail.TrailType = NormalizeOptionalText(trail.TrailType);
        existingTrail.Description = NormalizeOptionalText(trail.Description);
        existingTrail.Polyline = NormalizeOptionalText(trail.Polyline);
        existingTrail.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(existingTrail);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var trail = await _dbContext.Trails.FindAsync(id);
        if (trail is null)
        {
            return NotFound();
        }

        _dbContext.Trails.Remove(trail);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsValidTrail(Trail trail, out string? validationError)
    {
        if (trail is null)
        {
            validationError = "Der Trail darf nicht leer sein.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(trail.Name))
        {
            validationError = "Der Name ist erforderlich.";
            return false;
        }

        if (trail.Length < 0)
        {
            validationError = "Die Länge darf nicht negativ sein.";
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
