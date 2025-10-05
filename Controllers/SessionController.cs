using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ai_meetv2.Data;
using ai_meetv2.Services;
using ai_meetv2.Models;
using DbConversationMessage = ai_meetv2.Models.ConversationMessage;
using DbCorrection = ai_meetv2.Models.Correction;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _tokenService;
    private readonly AdaptiveLearningService _adaptiveService;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        AppDbContext context, 
        JwtTokenService tokenService,
        AdaptiveLearningService adaptiveService,
        ILogger<SessionController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _adaptiveService = adaptiveService;
        _logger = logger;
    }

    private int? GetUserId()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        return string.IsNullOrEmpty(token) ? null : _tokenService.ValidateToken(token);
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            // Get adapted difficulty level
            var difficulty = await _adaptiveService.GetAdaptedDifficultyLevel(userId.Value, request.TargetLanguage);

            var session = new LearningSession
            {
                UserId = userId.Value,
                StartTime = DateTime.UtcNow,
                TargetLanguage = request.TargetLanguage,
                Topic = request.Topic ?? "General Conversation",
                Mode = request.Mode ?? "Casual",
                DifficultyLevel = difficulty
            };

            _context.LearningSessions.Add(session);
            await _context.SaveChangesAsync();

            // Get personalized prompt
            var systemPrompt = await _adaptiveService.GeneratePersonalizedPrompt(
                userId.Value, 
                request.TargetLanguage, 
                session.Topic);

            return Ok(new
            {
                SessionId = session.Id,
                DifficultyLevel = difficulty,
                SystemPrompt = systemPrompt,
                Message = "Session started successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting session");
            return StatusCode(500, "Failed to start session");
        }
    }

    [HttpPost("{sessionId}/message")]
    public async Task<IActionResult> AddMessage(int sessionId, [FromBody] AddMessageRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var session = await _context.LearningSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null) return NotFound("Session not found");

            var message = new DbConversationMessage
            {
                SessionId = sessionId,
                IsUser = request.IsUser,
                Text = request.Text,
                Language = request.Language ?? session.TargetLanguage,
                Timestamp = DateTime.UtcNow,
                TranscriptionConfidence = request.Confidence
            };

            _context.ConversationMessages.Add(message);
            session.TotalMessages++;
            await _context.SaveChangesAsync();

            return Ok(new { MessageId = message.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding message");
            return StatusCode(500, "Failed to add message");
        }
    }

    [HttpPost("{sessionId}/correction")]
    public async Task<IActionResult> AddCorrection(int sessionId, [FromBody] AddCorrectionRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var session = await _context.LearningSessions
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null) return NotFound("Session not found");

            var correction = new DbCorrection
            {
                SessionId = sessionId,
                OriginalText = request.OriginalText,
                CorrectedText = request.CorrectedText,
                ErrorType = request.ErrorType,
                Explanation = request.Explanation,
                Severity = request.Severity ?? "Medium",
                Timestamp = DateTime.UtcNow
            };

            _context.Corrections.Add(correction);
            session.CorrectionsGiven++;
            await _context.SaveChangesAsync();

            return Ok(new { CorrectionId = correction.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding correction");
            return StatusCode(500, "Failed to add correction");
        }
    }

    [HttpPost("{sessionId}/end")]
    public async Task<IActionResult> EndSession(int sessionId, [FromBody] EndSessionRequest request)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var session = await _context.LearningSessions
                .Include(s => s.Messages)
                .Include(s => s.Corrections)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null) return NotFound("Session not found");

            session.EndTime = DateTime.UtcNow;
            session.DurationMinutes = (int)(session.EndTime.Value - session.StartTime).TotalMinutes;
            session.AccuracyScore = request.AccuracyScore ?? 75.0;
            session.ConversationSummary = request.Summary ?? "";
            session.VocabularyList = request.VocabularyList ?? "";
            session.CommonMistakes = request.CommonMistakes ?? "";

            await _context.SaveChangesAsync();

            // Update user progress
            await _adaptiveService.UpdateUserProgress(userId.Value, sessionId);

            // Get session summary
            var summary = new
            {
                session.DurationMinutes,
                session.TotalMessages,
                session.CorrectionsGiven,
                session.AccuracyScore,
                session.DifficultyLevel,
                MessageCount = session.Messages.Count,
                CorrectionCount = session.Corrections.Count,
                TopMistakes = session.Corrections
                    .GroupBy(c => c.ErrorType)
                    .Select(g => new { Type = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .Take(3)
                    .ToList()
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session");
            return StatusCode(500, "Failed to end session");
        }
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetSessionHistory([FromQuery] int limit = 10)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var sessions = await _context.LearningSessions
                .Where(s => s.UserId == userId && s.EndTime != null)
                .OrderByDescending(s => s.StartTime)
                .Take(limit)
                .Select(s => new
                {
                    s.Id,
                    s.StartTime,
                    s.EndTime,
                    s.DurationMinutes,
                    s.TargetLanguage,
                    s.Topic,
                    s.TotalMessages,
                    s.CorrectionsGiven,
                    s.AccuracyScore,
                    s.DifficultyLevel
                })
                .ToListAsync();

            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session history");
            return StatusCode(500, "Failed to get session history");
        }
    }

    [HttpGet("{sessionId}/details")]
    public async Task<IActionResult> GetSessionDetails(int sessionId)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var session = await _context.LearningSessions
                .Include(s => s.Messages)
                .Include(s => s.Corrections)
                .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);

            if (session == null) return NotFound("Session not found");

            return Ok(new
            {
                session.Id,
                session.StartTime,
                session.EndTime,
                session.DurationMinutes,
                session.TargetLanguage,
                session.Topic,
                session.Mode,
                session.DifficultyLevel,
                session.TotalMessages,
                session.CorrectionsGiven,
                session.AccuracyScore,
                session.ConversationSummary,
                Messages = session.Messages.Select(m => new
                {
                    m.Id,
                    m.Timestamp,
                    m.IsUser,
                    m.Text,
                    m.Language
                }).ToList(),
                Corrections = session.Corrections.Select(c => new
                {
                    c.Id,
                    c.Timestamp,
                    c.OriginalText,
                    c.CorrectedText,
                    c.ErrorType,
                    c.Explanation,
                    c.Severity
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting session details");
            return StatusCode(500, "Failed to get session details");
        }
    }
}

public class StartSessionRequest
{
    public string TargetLanguage { get; set; } = "en-US";
    public string? Topic { get; set; }
    public string? Mode { get; set; }
}

public class AddMessageRequest
{
    public bool IsUser { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
    public string? Confidence { get; set; }
}

public class AddCorrectionRequest
{
    public string OriginalText { get; set; } = string.Empty;
    public string CorrectedText { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string? Severity { get; set; }
}

public class EndSessionRequest
{
    public double? AccuracyScore { get; set; }
    public string? Summary { get; set; }
    public string? VocabularyList { get; set; }
    public string? CommonMistakes { get; set; }
}
