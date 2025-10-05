using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DIDController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DIDController> _logger;

    public DIDController(HttpClient httpClient, IConfiguration configuration, ILogger<DIDController> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("create-talk")]
    public async Task<IActionResult> CreateTalk([FromBody] CreateTalkRequest request)
    {
        try
        {
            var apiKey = _configuration["DID:ApiKey"];
            var apiUrl = _configuration["DID:ApiUrl"];
            var defaultPresenter = _configuration["DID:DefaultPresenter"];

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_DID_API_KEY_HERE")
            {
                return BadRequest(new { error = "D-ID API key not configured. Please add your D-ID API key to appsettings.json" });
            }

            _logger.LogInformation("Creating D-ID talk for text: {Text}", request.Text?.Substring(0, Math.Min(50, request.Text?.Length ?? 0)));

            // Prepare the request to D-ID API
            var didRequest = new
            {
                source_url = request.PresenterImageUrl ?? GetDefaultPresenterUrl(defaultPresenter),
                script = new
                {
                    type = "text",
                    input = request.Text,
                    provider = new
                    {
                        type = "microsoft",
                        voice_id = GetVoiceIdForLanguage(request.Language ?? "en-US")
                    }
                },
                config = new
                {
                    fluent = true,
                    pad_audio = 0.0,
                    stitch = true
                }
            };

            var json = JsonSerializer.Serialize(didRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {apiKey}");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.PostAsync($"{apiUrl}/talks", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("D-ID API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return StatusCode((int)response.StatusCode, new { error = "Failed to create D-ID talk", details = responseContent });
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var talkId = result.GetProperty("id").GetString();

            _logger.LogInformation("D-ID talk created successfully: {TalkId}", talkId);

            return Ok(new
            {
                id = talkId,
                status = "created"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating D-ID talk");
            return StatusCode(500, new { error = "Error creating D-ID talk", message = ex.Message });
        }
    }

    [HttpGet("talk/{talkId}")]
    public async Task<IActionResult> GetTalk(string talkId)
    {
        try
        {
            var apiKey = _configuration["DID:ApiKey"];
            var apiUrl = _configuration["DID:ApiUrl"];

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_DID_API_KEY_HERE")
            {
                return BadRequest(new { error = "D-ID API key not configured" });
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {apiKey}");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.GetAsync($"{apiUrl}/talks/{talkId}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("D-ID API error getting talk: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return StatusCode((int)response.StatusCode, new { error = "Failed to get D-ID talk", details = responseContent });
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            var status = result.GetProperty("status").GetString();

            // Extract video URL if available
            string? videoUrl = null;
            if (status == "done" && result.TryGetProperty("result_url", out var urlElement))
            {
                videoUrl = urlElement.GetString();
            }

            return Ok(new
            {
                id = talkId,
                status = status,
                result_url = videoUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting D-ID talk");
            return StatusCode(500, new { error = "Error getting D-ID talk", message = ex.Message });
        }
    }

    [HttpPost("create-stream")]
    public async Task<IActionResult> CreateStream([FromBody] CreateStreamRequest request)
    {
        try
        {
            var apiKey = _configuration["DID:ApiKey"];
            var apiUrl = _configuration["DID:ApiUrl"];
            var defaultPresenter = _configuration["DID:DefaultPresenter"];

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_DID_API_KEY_HERE")
            {
                return BadRequest(new { error = "D-ID API key not configured" });
            }

            _logger.LogInformation("Creating D-ID stream");

            var didRequest = new
            {
                source_url = request.PresenterImageUrl ?? GetDefaultPresenterUrl(defaultPresenter)
            };

            var json = JsonSerializer.Serialize(didRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {apiKey}");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.PostAsync($"{apiUrl}/talks/streams", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("D-ID API error creating stream: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return StatusCode((int)response.StatusCode, new { error = "Failed to create D-ID stream", details = responseContent });
            }

            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating D-ID stream");
            return StatusCode(500, new { error = "Error creating D-ID stream", message = ex.Message });
        }
    }

    [HttpDelete("stream/{streamId}")]
    public async Task<IActionResult> DeleteStream(string streamId)
    {
        try
        {
            var apiKey = _configuration["DID:ApiKey"];
            var apiUrl = _configuration["DID:ApiUrl"];

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_DID_API_KEY_HERE")
            {
                return BadRequest(new { error = "D-ID API key not configured" });
            }

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {apiKey}");

            var response = await _httpClient.DeleteAsync($"{apiUrl}/talks/streams/{streamId}");
            
            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("D-ID API error deleting stream: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return StatusCode((int)response.StatusCode, new { error = "Failed to delete D-ID stream" });
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting D-ID stream");
            return StatusCode(500, new { error = "Error deleting D-ID stream", message = ex.Message });
        }
    }

    private string GetDefaultPresenterUrl(string? presenterId)
    {
        // D-ID has several default presenters you can use
        // Format: https://d-id-public-bucket.s3.amazonaws.com/or-roman.jpg
        return presenterId switch
        {
            "amy-jcwCkr1grs" => "https://d-id-public-bucket.s3.us-west-2.amazonaws.com/alice.jpg",
            "anna" => "https://d-id-public-bucket.s3.us-west-2.amazonaws.com/anna.jpg",
            _ => "https://d-id-public-bucket.s3.us-west-2.amazonaws.com/or-roman.jpg"
        };
    }

    private string GetVoiceIdForLanguage(string language)
    {
        // Map language codes to Microsoft Azure TTS voices
        return language switch
        {
            "en-US" => "en-US-JennyNeural",
            "en-GB" => "en-GB-SoniaNeural",
            "ar-SA" => "ar-SA-ZariyahNeural",
            "te-IN" => "te-IN-ShrutiNeural",
            "ta-IN" => "ta-IN-PallaviNeural",
            "es-ES" => "es-ES-ElviraNeural",
            "fr-FR" => "fr-FR-DeniseNeural",
            "de-DE" => "de-DE-KatjaNeural",
            _ => "en-US-JennyNeural"
        };
    }
}

public class CreateTalkRequest
{
    public string? Text { get; set; }
    public string? Language { get; set; }
    public string? PresenterImageUrl { get; set; }
}

public class CreateStreamRequest
{
    public string? PresenterImageUrl { get; set; }
}
