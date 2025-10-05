namespace ai_meetv2.Models;

public class LearningSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public int DurationMinutes { get; set; } = 0;
    
    public string TargetLanguage { get; set; } = "en-US";
    public string Topic { get; set; } = "General Conversation";
    public string Mode { get; set; } = "Casual"; // Casual, Lesson, Practice
    
    // Session metrics
    public int TotalMessages { get; set; } = 0;
    public int CorrectionsGiven { get; set; } = 0;
    public int NewVocabularyLearned { get; set; } = 0;
    public double AccuracyScore { get; set; } = 0.0;
    public string DifficultyLevel { get; set; } = "Medium";
    
    // Session data
    public string ConversationSummary { get; set; } = string.Empty;
    public string VocabularyList { get; set; } = string.Empty; // JSON array
    public string CommonMistakes { get; set; } = string.Empty; // JSON array
    public string ImprovementAreas { get; set; } = string.Empty; // JSON array
    
    // Navigation properties
    public List<ConversationMessage> Messages { get; set; } = new();
    public List<Correction> Corrections { get; set; } = new();
}
