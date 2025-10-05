namespace ai_meetv2.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string NativeLanguage { get; set; } = "en-US";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    // Navigation properties
    public UserProfile? Profile { get; set; }
    public List<LearningSession> Sessions { get; set; } = new();
    public List<UserProgress> ProgressRecords { get; set; } = new();
}
