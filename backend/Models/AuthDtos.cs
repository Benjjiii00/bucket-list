namespace BucketListAPI.Models;

public record RegisterRequest(string Email, string Password, string ConfirmPassword);

public record LoginRequest(string Email, string Password);

public record AuthResponse(int UserId, string Email, string Token);

public record ErrorResponse(string Message, Dictionary<string, string>? Errors = null);
