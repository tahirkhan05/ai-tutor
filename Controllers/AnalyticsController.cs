using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ai_meetv2.Data;
using ai_meetv2.Services;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtTokenService _tokenService;
    private readonly AdaptiveLearningService _adaptiveService;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        AppDbContext context,
        JwtTokenService tokenService,
        AdaptiveLearningService adaptiveService,
        ILogger<AnalyticsController> logger)
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

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] string? language = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var targetLanguage = language ?? profile?.TargetLanguage ?? "English";
            
            _logger.LogInformation("Dashboard request for userId: {UserId}, targetLanguage: {Language}", userId, targetLanguage);

            // Overall stats - Count ALL sessions for this user regardless of language
            var allSessions = await _context.LearningSessions
                .Where(s => s.UserId == userId)
                .ToListAsync();
            
            _logger.LogInformation("Found {Count} total sessions for user", allSessions.Count);

            var totalSessions = allSessions.Count;
            var totalMinutes = allSessions.Sum(s => s.DurationMinutes);
            var completedSessions = allSessions.Where(s => s.EndTime != null).ToList();
            var avgAccuracy = completedSessions.Any() 
                ? completedSessions.Average(s => s.AccuracyScore) 
                : 0;

            _logger.LogInformation("Stats: Sessions={Sessions}, Minutes={Minutes}, Accuracy={Accuracy}", 
                totalSessions, totalMinutes, avgAccuracy);

            // Weekly progress
            var weekAgo = DateTime.UtcNow.AddDays(-7);
            var weeklyProgress = await _context.UserProgress
                .Where(p => p.UserId == userId && p.Date >= weekAgo.Date)
                .OrderBy(p => p.Date)
                .Select(p => new
                {
                    p.Date,
                    p.MinutesLearned,
                    p.SessionsCompleted,
                    p.AverageAccuracy,
                    p.GrammarScore,
                    p.VocabularyScore,
                    p.PronunciationScore,
                    p.FluencyScore
                })
                .ToListAsync();

            // Current week streak
            var currentStreak = await CalculateStreak(userId.Value, targetLanguage);

            // Weak areas
            var weakAreas = await _adaptiveService.IdentifyWeakAreas(userId.Value, targetLanguage);

            // Recent corrections
            var recentCorrections = await _context.Corrections
                .Include(c => c.Session)
                .Where(c => c.Session.UserId == userId && 
                           c.Session.TargetLanguage == targetLanguage)
                .OrderByDescending(c => c.Timestamp)
                .Take(5)
                .Select(c => new
                {
                    c.Id,
                    c.Timestamp,
                    c.OriginalText,
                    c.CorrectedText,
                    c.ErrorType,
                    c.Explanation,
                    c.Severity
                })
                .ToListAsync();

            return Ok(new
            {
                Overview = new
                {
                    TotalSessions = totalSessions,
                    TotalMinutes = totalMinutes,
                    AverageAccuracy = Math.Round(avgAccuracy, 2),
                    CurrentStreak = currentStreak,
                    ProficiencyLevel = profile?.ProficiencyLevel ?? "Beginner",
                    CurrentLevel = profile?.CurrentLevel ?? 1
                },
                WeeklyProgress = weeklyProgress,
                WeakAreas = weakAreas,
                RecentCorrections = recentCorrections
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard");
            return StatusCode(500, "Failed to get dashboard data");
        }
    }

    [HttpGet("progress")]
    public async Task<IActionResult> GetProgress([FromQuery] int days = 30, [FromQuery] string? language = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var targetLanguage = language ?? profile?.TargetLanguage ?? "en-US";

            var startDate = DateTime.UtcNow.AddDays(-days).Date;
            var progress = await _context.UserProgress
                .Where(p => p.UserId == userId && 
                           p.TargetLanguage == targetLanguage && 
                           p.Date >= startDate)
                .OrderBy(p => p.Date)
                .ToListAsync();

            // Fill missing days with zeros
            var completeProgress = new List<object>();
            for (int i = 0; i < days; i++)
            {
                var date = DateTime.UtcNow.AddDays(-days + i + 1).Date;
                var dayProgress = progress.FirstOrDefault(p => p.Date == date);

                completeProgress.Add(new
                {
                    Date = date.ToString("yyyy-MM-dd"),
                    MinutesLearned = dayProgress?.MinutesLearned ?? 0,
                    SessionsCompleted = dayProgress?.SessionsCompleted ?? 0,
                    MessagesSpoken = dayProgress?.MessagesSpoken ?? 0,
                    CorrectionsReceived = dayProgress?.CorrectionsReceived ?? 0,
                    AverageAccuracy = dayProgress?.AverageAccuracy ?? 0,
                    GrammarScore = dayProgress?.GrammarScore ?? 0,
                    VocabularyScore = dayProgress?.VocabularyScore ?? 0,
                    PronunciationScore = dayProgress?.PronunciationScore ?? 0,
                    FluencyScore = dayProgress?.FluencyScore ?? 0
                });
            }

            return Ok(completeProgress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting progress");
            return StatusCode(500, "Failed to get progress data");
        }
    }

    [HttpGet("weak-areas")]
    public async Task<IActionResult> GetWeakAreas([FromQuery] string? language = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var targetLanguage = language ?? profile?.TargetLanguage ?? "en-US";

            var weakAreas = await _adaptiveService.IdentifyWeakAreas(userId.Value, targetLanguage);

            var detailedWeakAreas = await _context.Corrections
                .Include(c => c.Session)
                .Where(c => c.Session.UserId == userId && 
                           c.Session.TargetLanguage == targetLanguage &&
                           c.Session.StartTime > DateTime.UtcNow.AddDays(-30))
                .GroupBy(c => c.ErrorType)
                .Select(g => new
                {
                    ErrorType = g.Key,
                    Count = g.Count(),
                    Examples = g.OrderByDescending(c => c.Timestamp)
                        .Take(3)
                        .Select(c => new
                        {
                            c.OriginalText,
                            c.CorrectedText,
                            c.Explanation,
                            c.Timestamp
                        })
                        .ToList()
                })
                .OrderByDescending(g => g.Count)
                .ToListAsync();

            return Ok(detailedWeakAreas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weak areas");
            return StatusCode(500, "Failed to get weak areas");
        }
    }

    [HttpGet("vocabulary")]
    public async Task<IActionResult> GetVocabulary([FromQuery] string? language = null)
    {
        try
        {
            var userId = GetUserId();
            if (userId == null) return Unauthorized("Invalid token");

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            var targetLanguage = language ?? profile?.TargetLanguage ?? "en-US";

            var sessions = await _context.LearningSessions
                .Where(s => s.UserId == userId && 
                           s.TargetLanguage == targetLanguage &&
                           !string.IsNullOrEmpty(s.VocabularyList))
                .OrderByDescending(s => s.StartTime)
                .Take(20)
                .Select(s => new
                {
                    s.Id,
                    s.StartTime,
                    s.Topic,
                    s.VocabularyList
                })
                .ToListAsync();

            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vocabulary");
            return StatusCode(500, "Failed to get vocabulary");
        }
    }

    private async Task<int> CalculateStreak(int userId, string targetLanguage)
    {
        var today = DateTime.UtcNow.Date;
        var streak = 0;

        for (int i = 0; i < 365; i++) // Max 1 year streak
        {
            var checkDate = today.AddDays(-i);
            var hasSession = await _context.LearningSessions
                .AnyAsync(s => s.UserId == userId && 
                              s.TargetLanguage == targetLanguage &&
                              s.StartTime.Date == checkDate);

            if (hasSession)
            {
                streak++;
            }
            else if (i > 0) // Allow missing today
            {
                break;
            }
        }

        return streak;
    }
}
