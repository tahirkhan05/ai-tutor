namespace ai_meetv2.Models;

public class Correction
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public LearningSession Session { get; set; } = null!;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string OriginalText { get; set; } = string.Empty;
    public string CorrectedText { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty; // Grammar, Vocabulary, Pronunciation, etc.
    public string Explanation { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium"; // Low, Medium, High
    public bool IsResolved { get; set; } = false;
}
