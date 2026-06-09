using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using BucketListAPI.Models;
using BucketListAPI.ORM;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BucketListAPI.Controllers;

using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly BucketListDbContext _context;
    private readonly IConfiguration _config;
    private const int PasswordMinLength = 8;

    public AuthController(BucketListDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var errors = new Dictionary<string, string>();

        // Validierung
        if (string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = "E-Mail ist erforderlich";
        else if (!IsValidEmail(request.Email))
            errors["email"] = "Ungültiges E-Mail-Format";

        if (string.IsNullOrWhiteSpace(request.Password))
            errors["password"] = "Passwort ist erforderlich";
        else if (request.Password.Length < PasswordMinLength)
            errors["password"] = $"Passwort muss mindestens {PasswordMinLength} Zeichen lang sein";

        if (request.Password != request.ConfirmPassword)
            errors["confirmPassword"] = "Passwörter stimmen nicht überein";

        if (errors.Any())
            return BadRequest(new ErrorResponse("Validierungsfehler", errors));

        // E-Mail bereits vorhanden?
        if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            return Conflict(new ErrorResponse("Diese E-Mail ist bereits registriert"));

        // User erstellen
        var user = new User
        {
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponse(user.Id, user.Email, token));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors["email"] = "E-Mail ist erforderlich";

        if (string.IsNullOrWhiteSpace(request.Password))
            errors["password"] = "Passwort ist erforderlich";

        if (errors.Any())
            return BadRequest(new ErrorResponse("Validierungsfehler", errors));

        // User suchen
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            return Unauthorized(new ErrorResponse("E-Mail oder Passwort sind falsch"));

        var token = GenerateJwtToken(user);

        return Ok(new AuthResponse(user.Id, user.Email, token));
    }

    // Hilfsmethoden
    private static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == hash;
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSecret = _config["Jwt:Secret"];
        if (string.IsNullOrEmpty(jwtSecret) || jwtSecret.Length < 32)
            throw new InvalidOperationException("JWT Secret muss in appsettings.json konfiguriert sein und mindestens 32 Zeichen lang");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"] ?? "bucket-list-api",
            audience: _config["Jwt:Audience"] ?? "bucket-list-client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
