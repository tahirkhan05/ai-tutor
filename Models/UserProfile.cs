namespace ai_meetv2.Models;

public class UserProfile
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    // Learning preferences
    public string TargetLanguage { get; set; } = "en-US";
    public string ProficiencyLevel { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced
    public int CurrentLevel { get; set; } = 1; // 1-10 scale
    
    // Learning focus areas
    public string FocusAreas { get; set; } = "Grammar,Vocabulary,Pronunciation"; // CSV
    public string LearningGoals { get; set; } = string.Empty;
    
    // Statistics
    public int TotalSessions { get; set; } = 0;
    public int TotalMinutesLearned { get; set; } = 0;
    public int TotalCorrections { get; set; } = 0;
    public double AverageAccuracy { get; set; } = 0.0;
    
    // Adaptive learning data
    public string WeakAreas { get; set; } = string.Empty; // JSON array of weak topics
    public string MasteredTopics { get; set; } = string.Empty; // JSON array
    public DateTime LastSessionDate { get; set; } = DateTime.UtcNow;
}
