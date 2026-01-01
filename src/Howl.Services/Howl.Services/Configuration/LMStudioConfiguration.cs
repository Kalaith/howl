namespace Howl.Services.Configuration;

public class LMStudioConfiguration
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:1234";
    public string Model { get; set; } = "zai-org/glm-4.6v-flash"; // Vision-capable model
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetries { get; set; } = 3;
    public bool EnableVision { get; set; } = true; // Send screenshots to vision-capable models
    public int MaxImageWidth { get; set; } = 1024; // Resize images to reduce token usage
    public int JpegQuality { get; set; } = 75; // JPEG compression quality (1-100)
    public bool SendPreviousScreenshot { get; set; } = false; // Send both previous and current (uses more tokens)
}
