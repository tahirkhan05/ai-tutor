using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using ai_meetv2.Services;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GeminiChatController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiChatController> _logger;
    private readonly AdaptiveLearningService? _adaptiveService;
    private readonly JwtTokenService? _tokenService;

    public GeminiChatController(
        HttpClient httpClient, 
        IConfiguration configuration, 
        ILogger<GeminiChatController> logger,
        AdaptiveLearningService? adaptiveService = null,
        JwtTokenService? tokenService = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _adaptiveService = adaptiveService;
        _tokenService = tokenService;
    }

    private int? GetUserId()
    {
        var token = Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
        return string.IsNullOrEmpty(token) || _tokenService == null 
            ? null 
            : _tokenService.ValidateToken(token);
    }

    [HttpPost("analyze")]
    public async Task<IActionResult> AnalyzeSpeech([FromBody] AnalyzeSpeechRequest request)
    {
        try
        {
            var apiKey = _configuration["GeminiAI:ApiKey"];
            var model = _configuration["GeminiAI:Model"] ?? "gemini-2.5-flash";

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GEMINI_API_KEY")
            {
                return BadRequest("Gemini API key not configured. Please add your Gemini API key to appsettings.json");
            }

            var systemPrompt = GetSystemPrompt(request.TargetLanguage);
            var userPrompt = $"Please analyze this speech and provide feedback: '{request.SpokenText}'";

            var response = await CallGeminiAPI(apiKey, model, systemPrompt, userPrompt);

            return Ok(new
            {
                Feedback = response,
                Corrections = ExtractCorrections(response),
                Encouragement = ExtractEncouragement(response)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing speech with Gemini");
            return StatusCode(500, "Error analyzing speech");
        }
    }

    [HttpPost("conversation")]
    public async Task<IActionResult> ContinueConversation([FromBody] ConversationRequest request)
    {
        try
        {
            _logger.LogInformation("Conversation request received: {UserMessage}", request.UserMessage);
            
            var apiKey = _configuration["GeminiAI:ApiKey"];
            var model = _configuration["GeminiAI:Model"] ?? "gemini-2.5-flash";

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_GEMINI_API_KEY")
            {
                _logger.LogError("Gemini API key not configured");
                return BadRequest("Gemini API key not configured. Please add your Gemini API key to appsettings.json");
            }

            // Get user ID if authenticated
            var userId = GetUserId();
            
            // Get personalized system prompt if user is authenticated and adaptive service is available
            string systemPrompt;
            if (userId.HasValue && _adaptiveService != null && !string.IsNullOrEmpty(request.SessionId))
            {
                systemPrompt = await _adaptiveService.GeneratePersonalizedPrompt(
                    userId.Value, 
                    request.TargetLanguage, 
                    request.Topic ?? "General Conversation");
            }
            else
            {
                systemPrompt = GetConversationPrompt(request.TargetLanguage, request.Topic);
            }
            
            // Build conversation context
            var conversationContext = new StringBuilder();
            foreach (var msg in request.ConversationHistory.TakeLast(6)) // Last 6 messages for context
            {
                conversationContext.AppendLine($"{(msg.IsUser ? "Student" : "Tutor")}: {msg.Text}");
            }
            
            var userPrompt = $"Conversation so far:\n{conversationContext}\nStudent: {request.UserMessage}\n\nPlease respond as the tutor:";

            _logger.LogInformation("Calling Gemini API...");
            var response = await CallGeminiAPI(apiKey, model, systemPrompt, userPrompt);
            _logger.LogInformation("Gemini API response received: {Response}", response);

            // Skip separate correction analysis to improve performance
            // Corrections can be included in the main response if needed
            var corrections = new List<CorrectionInfo>();

            return Ok(new
            {
                Response = response,
                Corrections = corrections,
                ShouldCorrect = corrections.Any(),
                NextTopic = SuggestNextTopic(request.Topic ?? "General Conversation")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Gemini conversation");
            return StatusCode(500, "Error in conversation: " + ex.Message);
        }
    }

    private async Task<List<CorrectionInfo>> AnalyzeForCorrections(string apiKey, string model, string userMessage, string targetLanguage)
    {
        try
        {
            var analysisPrompt = $@"Analyze this {targetLanguage} sentence for errors and provide corrections in JSON format:
""{userMessage}""

Return ONLY a JSON array of corrections in this exact format (return empty array [] if no errors):
[
  {{
    ""originalText"": ""the exact wrong part"",
    ""correctedText"": ""the corrected version"",
    ""errorType"": ""Grammar|Vocabulary|Pronunciation|Spelling"",
    ""explanation"": ""brief explanation"",
    ""severity"": ""Low|Medium|High""
  }}
]";

            var response = await CallGeminiAPI(apiKey, model, "You are a language error analyzer. Return only valid JSON.", analysisPrompt);
            
            _logger.LogInformation("Gemini correction analysis response: {Response}", response);
            
            // Extract JSON from response (sometimes Gemini adds extra text)
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                _logger.LogInformation("Extracted JSON: {Json}", jsonStr);
                
                // Use case-insensitive deserialization
                var options = new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                };
                var corrections = JsonSerializer.Deserialize<List<CorrectionInfo>>(jsonStr, options) ?? new List<CorrectionInfo>();
                _logger.LogInformation("Parsed {Count} corrections", corrections.Count);
                
                return corrections;
            }

            _logger.LogWarning("No JSON array found in Gemini response");
            return new List<CorrectionInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error analyzing for corrections");
            return new List<CorrectionInfo>();
        }
    }

    private async Task<string> CallGeminiAPI(string apiKey, string model, string systemPrompt, string userPrompt)
    {
        try
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 200  // Optimized for fast, concise responses
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            _logger.LogInformation("Calling Gemini API at: {Url}", url);
            _logger.LogInformation("Request body: {Json}", json);
            
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Gemini API error: {response.StatusCode} - {errorContent}");
                throw new Exception($"Gemini API call failed: {response.StatusCode} - {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Gemini response: {Response}", responseContent);
            
            var geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent);

            return geminiResponse?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text 
                   ?? "I apologize, I didn't understand that. Could you please try again?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Gemini API");
            throw;
        }
    }

    private string GetSystemPrompt(string targetLanguage)
    {
        return $@"You are an expert {targetLanguage} language tutor conducting a video call session. 
        Your role is to:
        1. Analyze the student's speech for grammar, vocabulary, and pronunciation
        2. Provide constructive, encouraging feedback
        3. Suggest improvements in a friendly manner
        4. Correct mistakes clearly but positively
        5. Ask follow-up questions to continue the conversation

        Always be patient, encouraging, and educational. Focus on making the student feel confident while learning.
        Provide specific examples of correct usage when giving corrections.
        Keep responses concise and conversational.";
    }

    private string GetConversationPrompt(string targetLanguage, string topic)
    {
        return $@"You are a friendly {targetLanguage} language tutor having a natural conversation about {topic}.
        Guidelines:
        - Keep responses conversational and at appropriate difficulty level
        - Ask engaging questions to keep the conversation flowing
        - If you notice ANY mistake, briefly mention it naturally in your response (e.g., 'By the way, we say X not Y')
        - Provide encouragement and positive reinforcement
        - Use simple, clear language
        - Stay on topic but allow natural conversation flow
        - Keep responses to 1-2 SHORT sentences maximum (under 30 words total)
        - Be concise and direct - avoid lengthy explanations
        - Corrections should be subtle and encouraging, not disruptive";
    }

    private List<string> ExtractCorrections(string feedback)
    {
        var corrections = new List<string>();
        if (feedback.Contains("correct") || feedback.Contains("should be") || feedback.Contains("better"))
        {
            corrections.Add("Grammar or vocabulary improvement suggested");
        }
        return corrections;
    }

    private string ExtractEncouragement(string feedback)
    {
        if (feedback.Contains("good") || feedback.Contains("great") || feedback.Contains("excellent") || feedback.Contains("well done"))
            return "Great job! Keep practicing!";
        
        return "You're doing well! Keep going!";
    }

    private bool ShouldCorrectSpeech(string userMessage, string targetLanguage)
    {
        return userMessage.Length > 5 && (
            userMessage.Contains("I are") ||
            userMessage.Contains("He have") ||
            userMessage.Contains("She do") ||
            userMessage.Contains("They is")
        );
    }

    private string SuggestNextTopic(string currentTopic)
    {
        var topics = new[] { "daily routines", "food and cooking", "travel experiences", "hobbies", "work life", "family", "weather", "movies" };
        var random = new Random();
        return topics[random.Next(topics.Length)];
    }
}

// Gemini API Response Models
public class GeminiResponse
{
    public GeminiCandidate[]? candidates { get; set; }
}

public class GeminiCandidate
{
    public GeminiContent? content { get; set; }
}

public class GeminiContent
{
    public GeminiPart[]? parts { get; set; }
}

public class GeminiPart
{
    public string? text { get; set; }
}

// Request Models (reused from ChatController)
public class AnalyzeSpeechRequest
{
    public string SpokenText { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "English";
    public string NativeLanguage { get; set; } = "English";
}

public class ConversationRequest
{
    public string UserMessage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = "English";
    public string Topic { get; set; } = "general conversation";
    public string? SessionId { get; set; }
    public List<ConversationMessage> ConversationHistory { get; set; } = new();
}

public class ConversationMessage
{
    public string Text { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class CorrectionInfo
{
    public string OriginalText { get; set; } = string.Empty;
    public string CorrectedText { get; set; } = string.Empty;
    public string ErrorType { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
}
