using System.Text.Json.Serialization;

namespace Howl.Services.Models;

// API request/response models
public class GeminiApiResponse
{
    [JsonPropertyName("candidates")]
    public Candidate[]? Candidates { get; set; }
}

public class Candidate
{
    [JsonPropertyName("content")]
    public Content? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

public class Content
{
    [JsonPropertyName("parts")]
    public Part[]? Parts { get; set; }
}

public class Part
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

// Expected instruction format from Gemini
public class GeminiInstructionResponse
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("prerequisites")]
    public string[]? Prerequisites { get; set; }

    [JsonPropertyName("steps")]
    public InstructionStep[]? Steps { get; set; }
}

public class InstructionStep
{
    [JsonPropertyName("stepNumber")]
    public int StepNumber { get; set; }

    [JsonPropertyName("instruction")]
    public string Instruction { get; set; } = string.Empty;

    [JsonPropertyName("screenshotReference")]
    public string? ScreenshotReference { get; set; } // Optional - will be populated from actual screenshot files
}

// Domain model
public class GeminiResponse
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string[]? Prerequisites { get; set; }
    public InstructionStep[]? Instructions { get; set; }
}
