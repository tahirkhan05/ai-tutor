using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ai_meetv2.Data;
using ai_meetv2.Models;
using ai_meetv2.Services;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(AppDbContext context, JwtTokenService tokenService, ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Email) || 
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest("Username, email, and password are required");
            }

            // Check if user exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email || u.Username == request.Username);

            if (existingUser != null)
            {
                return BadRequest("User with this email or username already exists");
            }

            // Create user
            var user = new User
            {
                Username = request.Username,
                Email = request.Email,
                PasswordHash = PasswordHasher.HashPassword(request.Password),
                NativeLanguage = request.NativeLanguage ?? "en-US",
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Create user profile
            var profile = new UserProfile
            {
                UserId = user.Id,
                TargetLanguage = request.TargetLanguage ?? "en-US",
                ProficiencyLevel = request.ProficiencyLevel ?? "Beginner",
                CurrentLevel = 1,
                FocusAreas = "Grammar,Vocabulary,Pronunciation"
            };

            _context.UserProfiles.Add(profile);
            await _context.SaveChangesAsync();

            // Generate token
            var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email);

            return Ok(new
            {
                Token = token,
                User = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    Profile = new
                    {
                        profile.TargetLanguage,
                        profile.ProficiencyLevel,
                        profile.CurrentLevel
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, "Registration failed");
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email == request.Email || u.Username == request.Email);

            if (user == null || !PasswordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Unauthorized("Invalid credentials");
            }

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generate token
            var token = _tokenService.GenerateToken(user.Id, user.Username, user.Email);

            return Ok(new
            {
                Token = token,
                User = new
                {
                    user.Id,
                    user.Username,
                    user.Email,
                    Profile = user.Profile != null ? new
                    {
                        user.Profile.TargetLanguage,
                        user.Profile.ProficiencyLevel,
                        user.Profile.CurrentLevel,
                        user.Profile.TotalSessions,
                        user.Profile.TotalMinutesLearned
                    } : null
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, "Login failed");
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        try
        {
            var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized("No token provided");
            }

            var userId = _tokenService.ValidateToken(token);
            if (userId == null)
            {
                return Unauthorized("Invalid token");
            }

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("User not found");
            }

            return Ok(new
            {
                user.Id,
                user.Username,
                user.Email,
                user.NativeLanguage,
                Profile = user.Profile != null ? new
                {
                    user.Profile.TargetLanguage,
                    user.Profile.ProficiencyLevel,
                    user.Profile.CurrentLevel,
                    user.Profile.TotalSessions,
                    user.Profile.TotalMinutesLearned,
                    user.Profile.TotalCorrections,
                    user.Profile.AverageAccuracy,
                    user.Profile.FocusAreas
                } : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current user");
            return StatusCode(500, "Failed to get user");
        }
    }
}

public class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? NativeLanguage { get; set; }
    public string? TargetLanguage { get; set; }
    public string? ProficiencyLevel { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
