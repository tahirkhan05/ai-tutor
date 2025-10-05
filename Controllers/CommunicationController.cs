using Microsoft.AspNetCore.Mvc;
using Azure.Communication.Identity;
using Azure.Communication;
using ai_meetv2.Services;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CommunicationController : ControllerBase
{
    private readonly CommunicationIdentityClient _identityClient;
    private readonly JwtTokenService _tokenService;
    private readonly ILogger<CommunicationController> _logger;

    public CommunicationController(
        CommunicationIdentityClient identityClient,
        JwtTokenService tokenService,
        ILogger<CommunicationController> logger)
    {
        _identityClient = identityClient;
        _tokenService = tokenService;
        _logger = logger;
    }

    [HttpGet("token")]
    public async Task<IActionResult> GetToken()
    {
        try
        {
            // Validate user (optional - can be used for guest access too)
            var authToken = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            var userId = !string.IsNullOrEmpty(authToken) ? _tokenService.ValidateToken(authToken) : null;

            _logger.LogInformation($"Generating ACS token for user: {userId?.ToString() ?? "guest"}");

            // Create an identity
            var identityResponse = await _identityClient.CreateUserAsync();
            var identity = identityResponse.Value;

            // Issue token with video calling scope
            var tokenResponse = await _identityClient.GetTokenAsync(
                identity,
                scopes: new[] { CommunicationTokenScope.VoIP }
            );

            return Ok(new
            {
                Token = tokenResponse.Value.Token,
                ExpiresOn = tokenResponse.Value.ExpiresOn,
                UserId = identity.Id,
                Message = "Communication token generated successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating communication token");
            return StatusCode(500, "Failed to generate communication token: " + ex.Message);
        }
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                return BadRequest("UserId is required");
            }

            var identity = new CommunicationUserIdentifier(request.UserId);

            var tokenResponse = await _identityClient.GetTokenAsync(
                identity,
                scopes: new[] { CommunicationTokenScope.VoIP }
            );

            return Ok(new
            {
                Token = tokenResponse.Value.Token,
                ExpiresOn = tokenResponse.Value.ExpiresOn,
                Message = "Token refreshed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing communication token");
            return StatusCode(500, "Failed to refresh token");
        }
    }

    [HttpDelete("revoke/{userId}")]
    public async Task<IActionResult> RevokeToken(string userId)
    {
        try
        {
            var identity = new CommunicationUserIdentifier(userId);
            await _identityClient.RevokeTokensAsync(identity);

            return Ok(new { Message = "Tokens revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking tokens");
            return StatusCode(500, "Failed to revoke tokens");
        }
    }
}

public class RefreshTokenRequest
{
    public string UserId { get; set; } = string.Empty;
}
