namespace ai_meetv2.Models;

public class ConversationMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public LearningSession Session { get; set; } = null!;
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool IsUser { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "en-US";
    
    // Analysis data
    public string? TranscriptionConfidence { get; set; }
    public string? SentimentScore { get; set; }
    public bool HasError { get; set; } = false;
}
