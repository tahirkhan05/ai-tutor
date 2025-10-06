using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Text;

namespace ai_meetv2.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeechController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SpeechController> _logger;

    public SpeechController(IConfiguration configuration, ILogger<SpeechController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("synthesize")]
    public async Task<IActionResult> SynthesizeSpeech([FromBody] SynthesizeSpeechRequest request)
    {
        try
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var requestId = Guid.NewGuid().ToString("N").Substring(0, 8);
            _logger.LogInformation($"[{timestamp}] [RequestID: {requestId}] /api/speech/synthesize called with text: {request.Text}");
            
            var speechKey = _configuration["AzureSpeech:Key"];
            var speechRegion = _configuration["AzureSpeech:Region"];

            var config = SpeechConfig.FromSubscription(speechKey!, speechRegion!);
            config.SpeechSynthesisVoiceName = GetVoiceForLanguage(request.Language);

            _logger.LogInformation($"[{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}] [RequestID: {requestId}] Calling Azure Speech Service...");
            
            // Configure to return audio data only, don't play to speakers
            using var synthesizer = new SpeechSynthesizer(config, null);
            
            // Use SSML for natural speech with optimized settings for speed
            var voiceName = GetVoiceForLanguage(request.Language);
            var ssml = $@"
                <speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' 
                       xmlns:mstts='https://www.w3.org/2001/mstts' xml:lang='en-US'>
                    <voice name='{voiceName}'>
                        <prosody rate='1.15'>
                            {System.Security.SecurityElement.Escape(request.Text)}
                        </prosody>
                    </voice>
                </speak>";
            
            var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation($"[{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}] [RequestID: {requestId}] Audio generated, size: {result.AudioData.Length} bytes");
                return base.File(result.AudioData, "audio/wav", "speech.wav");
            }
            else
            {
                _logger.LogError($"[{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}] [RequestID: {requestId}] Speech synthesis failed: {result.Reason}");
                return BadRequest("Speech synthesis failed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error synthesizing speech");
            return StatusCode(500, "Error synthesizing speech");
        }
    }

    [HttpPost("recognize")]
    public async Task<IActionResult> RecognizeSpeech([FromForm] IFormFile audioFile, [FromForm] string language = "en-US")
    {
        try
        {
            var speechKey = _configuration["AzureSpeech:Key"];
            var speechRegion = _configuration["AzureSpeech:Region"];

            var config = SpeechConfig.FromSubscription(speechKey!, speechRegion!);
            config.SpeechRecognitionLanguage = language;

            // Create a push audio input stream
            var format = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);
            var pushStream = AudioInputStream.CreatePushStream(format);

            // Read the audio file and push to stream
            using var fileStream = audioFile.OpenReadStream();
            var buffer = new byte[1024];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                pushStream.Write(buffer, bytesRead);
            }
            pushStream.Close();

            using var audioConfig = AudioConfig.FromStreamInput(pushStream);
            using var recognizer = new SpeechRecognizer(config, audioConfig);

            var result = await recognizer.RecognizeOnceAsync();

            _logger.LogInformation($"Speech recognition result: {result.Reason}, Text: '{result.Text}'");

            if (result.Reason == ResultReason.RecognizedSpeech)
            {
                _logger.LogInformation($"✅ Speech recognized: '{result.Text}'");
                return Ok(new
                {
                    Text = result.Text,
                    Confidence = 0.95 // Placeholder confidence
                });
            }
            else if (result.Reason == ResultReason.NoMatch)
            {
                _logger.LogWarning("⚠️ No speech matched");
                return Ok(new
                {
                    Text = "",
                    Error = "No speech detected"
                });
            }
            else
            {
                _logger.LogError($"❌ Speech recognition failed: {result.Reason}");
                return BadRequest($"Speech recognition failed: {result.Reason}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recognizing speech");
            return StatusCode(500, "Error recognizing speech: " + ex.Message);
        }
    }

    [HttpGet("config")]
    public IActionResult GetSpeechConfig()
    {
        try
        {
            var speechKey = _configuration["AzureSpeech:Key"];
            var speechRegion = _configuration["AzureSpeech:Region"];

            if (string.IsNullOrEmpty(speechKey) || string.IsNullOrEmpty(speechRegion))
            {
                return BadRequest("Speech service not configured");
            }

            return Ok(new
            {
                Key = speechKey,
                Region = speechRegion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting speech config");
            return StatusCode(500, "Error getting speech config");
        }
    }

    [HttpGet("voices")]
    public IActionResult GetAvailableVoices()
    {
        var voices = new Dictionary<string, List<object>>
        {
            ["en-US"] = new List<object>
            {
                new { Name = "en-US-JennyNeural", Gender = "Female" },
                new { Name = "en-US-GuyNeural", Gender = "Male" }
            },
            ["ar-SA"] = new List<object>
            {
                new { Name = "ar-SA-ZariyahNeural", Gender = "Female" },
                new { Name = "ar-SA-HamedNeural", Gender = "Male" }
            },
            ["te-IN"] = new List<object>
            {
                new { Name = "te-IN-ShrutiNeural", Gender = "Female" },
                new { Name = "te-IN-MohanNeural", Gender = "Male" }
            },
            ["ta-IN"] = new List<object>
            {
                new { Name = "ta-IN-PallaviNeural", Gender = "Female" },
                new { Name = "ta-IN-ValluvarNeural", Gender = "Male" }
            }
        };

        return Ok(voices);
    }

    private string GetVoiceForLanguage(string language)
    {
        return language.ToLower() switch
        {
            "en-us" or "english" => "en-US-AvaMultilingualNeural",  // Most natural, conversational English
            "ar-sa" or "arabic" => "ar-SA-ZariyahNeural", 
            "te-in" or "telugu" => "te-IN-ShrutiNeural",
            "ta-in" or "tamil" => "ta-IN-PallaviNeural",
            _ => "en-US-AvaMultilingualNeural"
        };
    }
}

public class SynthesizeSpeechRequest
{
    public string Text { get; set; } = string.Empty;
    public string Language { get; set; } = "en-US";
    public string Voice { get; set; } = string.Empty;
}
