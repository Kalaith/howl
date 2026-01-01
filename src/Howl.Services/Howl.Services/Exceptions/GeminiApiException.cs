using System;

namespace Howl.Services.Exceptions;

public class GeminiApiException : Exception
{
    public int? StatusCode { get; }

    public GeminiApiException(string message) : base(message) { }

    public GeminiApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public GeminiApiException(string message, Exception innerException)
        : base(message, innerException) { }
}
