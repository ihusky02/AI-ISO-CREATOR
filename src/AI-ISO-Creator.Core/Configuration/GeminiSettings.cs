namespace AI_ISO_Creator.Core.Configuration;

/// <summary>
/// Configuration options for connecting to the Google Gemini AI REST API.
/// </summary>
public class GeminiSettings
{
    /// <summary>
    /// Google Generative Language API key.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Target Gemini model name (e.g. "gemini-flash-latest", "gemini-flash-lite-latest", "gemini-3.1-pro-preview").
    /// </summary>
    public string Model { get; set; } = "gemini-flash-latest";

    /// <summary>
    /// Base URL for the Google Generative Language REST endpoint.
    /// </summary>
    public string ApiUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
