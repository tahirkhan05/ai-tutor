using ai_meetv2.Data;
using ai_meetv2.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ai_meetv2.Services;

public class AdaptiveLearningService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdaptiveLearningService> _logger;

    public AdaptiveLearningService(AppDbContext context, ILogger<AdaptiveLearningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GetAdaptedDifficultyLevel(int userId, string targetLanguage)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return "Medium";

        // Analyze recent performance
        var recentSessions = await _context.LearningSessions
            .Where(s => s.UserId == userId && s.TargetLanguage == targetLanguage)
            .OrderByDescending(s => s.StartTime)
            .Take(5)
            .ToListAsync();

        if (!recentSessions.Any()) return profile.ProficiencyLevel;

        var avgAccuracy = recentSessions.Average(s => s.AccuracyScore);
        var avgCorrections = recentSessions.Average(s => s.CorrectionsGiven);

        // Adaptive logic
        if (avgAccuracy > 85 && avgCorrections < 3)
        {
            return IncreaseDifficulty(profile.ProficiencyLevel);
        }
        else if (avgAccuracy < 60 || avgCorrections > 8)
        {
            return DecreaseDifficulty(profile.ProficiencyLevel);
        }

        return profile.ProficiencyLevel;
    }

    private string IncreaseDifficulty(string current)
    {
        return current switch
        {
            "Beginner" => "Intermediate",
            "Intermediate" => "Advanced",
            _ => "Advanced"
        };
    }

    private string DecreaseDifficulty(string current)
    {
        return current switch
        {
            "Advanced" => "Intermediate",
            "Intermediate" => "Beginner",
            _ => "Beginner"
        };
    }

    public async Task<List<string>> IdentifyWeakAreas(int userId, string targetLanguage)
    {
        var corrections = await _context.Corrections
            .Include(c => c.Session)
            .Where(c => c.Session.UserId == userId && 
                       c.Session.TargetLanguage == targetLanguage &&
                       c.Session.StartTime > DateTime.UtcNow.AddDays(-30))
            .GroupBy(c => c.ErrorType)
            .Select(g => new { ErrorType = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToListAsync();

        return corrections.Select(c => c.ErrorType).ToList();
    }

    public async Task<string> GeneratePersonalizedPrompt(int userId, string targetLanguage, string topic)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        var weakAreas = await IdentifyWeakAreas(userId, targetLanguage);
        var difficulty = await GetAdaptedDifficultyLevel(userId, targetLanguage);

        var prompt = $@"You are an AI language tutor teaching {targetLanguage} to a {difficulty} level student.

Current Topic: {topic}
Student's Weak Areas: {string.Join(", ", weakAreas.Any() ? weakAreas : new List<string> { "None identified yet" })}
Focus Areas: {profile?.FocusAreas ?? "General"}

Guidelines:
- Adapt your language complexity to {difficulty} level
- Provide extra attention to: {string.Join(", ", weakAreas.Any() ? weakAreas : new List<string> { "overall improvement" })}
- Be encouraging and supportive
- Correct mistakes gently but clearly
- Ask follow-up questions to encourage conversation
- Use natural, conversational language";

        return prompt;
    }

    public async Task UpdateUserProgress(int userId, int sessionId)
    {
        var session = await _context.LearningSessions
            .Include(s => s.Messages)
            .Include(s => s.Corrections)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return;

        var today = DateTime.UtcNow.Date;
        var progress = await _context.UserProgress
            .FirstOrDefaultAsync(p => p.UserId == userId && 
                                     p.Date == today && 
                                     p.TargetLanguage == session.TargetLanguage);

        if (progress == null)
        {
            progress = new UserProgress
            {
                UserId = userId,
                Date = today,
                TargetLanguage = session.TargetLanguage
            };
            _context.UserProgress.Add(progress);
        }

        // Update metrics
        progress.SessionsCompleted++;
        progress.MinutesLearned += session.DurationMinutes;
        progress.MessagesSpoken += session.Messages.Count(m => m.IsUser);
        progress.CorrectionsReceived += session.Corrections.Count;
        progress.AverageAccuracy = (progress.AverageAccuracy * (progress.SessionsCompleted - 1) + session.AccuracyScore) / progress.SessionsCompleted;

        // Update skill scores based on corrections
        UpdateSkillScores(progress, session.Corrections.ToList());

        await _context.SaveChangesAsync();

        // Update user profile
        await UpdateUserProfile(userId, session);
    }

    private void UpdateSkillScores(UserProgress progress, List<Correction> corrections)
    {
        var grammarErrors = corrections.Count(c => c.ErrorType.Contains("Grammar", StringComparison.OrdinalIgnoreCase));
        var vocabErrors = corrections.Count(c => c.ErrorType.Contains("Vocabulary", StringComparison.OrdinalIgnoreCase));
        var pronunciationErrors = corrections.Count(c => c.ErrorType.Contains("Pronunciation", StringComparison.OrdinalIgnoreCase));

        var totalMessages = progress.MessagesSpoken > 0 ? progress.MessagesSpoken : 1;

        progress.GrammarScore = Math.Max(0, 100 - (grammarErrors * 100.0 / totalMessages * 10));
        progress.VocabularyScore = Math.Max(0, 100 - (vocabErrors * 100.0 / totalMessages * 10));
        progress.PronunciationScore = Math.Max(0, 100 - (pronunciationErrors * 100.0 / totalMessages * 10));
        progress.FluencyScore = progress.AverageAccuracy;
    }

    private async Task UpdateUserProfile(int userId, LearningSession session)
    {
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null) return;

        profile.TotalSessions++;
        profile.TotalMinutesLearned += session.DurationMinutes;
        profile.TotalCorrections += session.Corrections.Count;
        profile.LastSessionDate = DateTime.UtcNow;

        // Update proficiency level
        var newLevel = await GetAdaptedDifficultyLevel(userId, session.TargetLanguage);
        profile.ProficiencyLevel = newLevel;

        await _context.SaveChangesAsync();
    }
}
