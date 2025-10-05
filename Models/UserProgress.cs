namespace ai_meetv2.Models;

public class UserProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public DateTime Date { get; set; } = DateTime.UtcNow.Date;
    public string TargetLanguage { get; set; } = "en-US";
    
    // Daily metrics
    public int SessionsCompleted { get; set; } = 0;
    public int MinutesLearned { get; set; } = 0;
    public int MessagesSpoken { get; set; } = 0;
    public int CorrectionsReceived { get; set; } = 0;
    public int NewVocabulary { get; set; } = 0;
    public double AverageAccuracy { get; set; } = 0.0;
    
    // Skill scores (0-100)
    public double GrammarScore { get; set; } = 0.0;
    public double VocabularyScore { get; set; } = 0.0;
    public double PronunciationScore { get; set; } = 0.0;
    public double FluencyScore { get; set; } = 0.0;
}
