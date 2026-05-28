using BucketListAPI.Models;
using BucketListAPI.ORM;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BucketListAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlacesController : ControllerBase
{
    private readonly BucketListDbContext _dbContext;

    public PlacesController(BucketListDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<TravelPlace>>> GetAllAsync()
    {
        var places = await _dbContext.TravelPlaces
            .AsNoTracking()
            .OrderBy(place => place.Name)
            .ToListAsync();

        return Ok(places);
    }

    [HttpGet("{id:int}", Name = "GetPlaceById")]
    public async Task<ActionResult<TravelPlace>> GetByIdAsync(int id)
    {
        var place = await _dbContext.TravelPlaces.FindAsync(id);
        return place is null ? NotFound() : Ok(place);
    }

    [HttpPost]
    public async Task<ActionResult<TravelPlace>> CreateAsync([FromBody] TravelPlace place)
    {
        if (!IsValidPlace(place, out var validationError))
        {
            return BadRequest(validationError);
        }

        place.Id = 0;
        place.CreatedAtUtc = DateTime.UtcNow;
        place.UpdatedAtUtc = DateTime.UtcNow;

        _dbContext.TravelPlaces.Add(place);
        await _dbContext.SaveChangesAsync();

        return CreatedAtRoute("GetPlaceById", new { id = place.Id }, place);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TravelPlace>> UpdateAsync(int id, [FromBody] TravelPlace place)
    {
        if (!IsValidPlace(place, out var validationError))
        {
            return BadRequest(validationError);
        }

        var existingPlace = await _dbContext.TravelPlaces.FindAsync(id);
        if (existingPlace is null)
        {
            return NotFound();
        }

        existingPlace.Name = place.Name.Trim();
        existingPlace.Country = NormalizeOptionalText(place.Country);
        existingPlace.Region = NormalizeOptionalText(place.Region);
        existingPlace.Latitude = place.Latitude;
        existingPlace.Longitude = place.Longitude;
        existingPlace.Status = place.Status;
        existingPlace.Notes = NormalizeOptionalText(place.Notes);
        existingPlace.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        return Ok(existingPlace);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id)
    {
        var place = await _dbContext.TravelPlaces.FindAsync(id);
        if (place is null)
        {
            return NotFound();
        }

        _dbContext.TravelPlaces.Remove(place);
        await _dbContext.SaveChangesAsync();

        return NoContent();
    }

    private static bool IsValidPlace(TravelPlace place, out string? validationError)
    {
        if (place is null)
        {
            validationError = "Der Ort darf nicht leer sein.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(place.Name))
        {
            validationError = "Der Name ist erforderlich.";
            return false;
        }

        if (place.Latitude is < -90 or > 90)
        {
            validationError = "Der Breitengrad liegt außerhalb des gültigen Bereichs.";
            return false;
        }

        if (place.Longitude is < -180 or > 180)
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