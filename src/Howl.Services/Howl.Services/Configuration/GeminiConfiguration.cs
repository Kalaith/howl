using System;

namespace Howl.Services.Configuration;

public class GeminiConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.0-flash-exp";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";
    public int MaxRetries { get; set; } = 3;
    public int TimeoutSeconds { get; set; } = 30;
    public double Temperature { get; set; } = 0.3;
    public int MaxOutputTokens { get; set; } = 2048;

    public void LoadFromEnvironment()
    {
        ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not configured");
    }
}
