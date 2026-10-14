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
    /// Target Gemini model name (e.g. "gemini-2.5-flash", "gemini-2.5-pro").
    /// </summary>
    public string Model { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Base URL for the Google Generative Language REST endpoint.
    /// </summary>
    public string ApiUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}
