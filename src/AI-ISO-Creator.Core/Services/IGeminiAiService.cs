using AI_ISO_Creator.Core.Models;

namespace AI_ISO_Creator.Core.Services;

/// <summary>
/// Service interface for interfacing with the Gemini AI model to generate and refine ISO recipes.
/// </summary>
public interface IGeminiAiService
{
    /// <summary>
    /// Generates a structured ISO recipe from a natural language user prompt.
    /// </summary>
    Task<IsoRecipe> GenerateRecipeAsync(string prompt, string? customModel = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refines an existing recipe according to user follow-up instructions.
    /// </summary>
    Task<IsoRecipe> RefineRecipeAsync(IsoRecipe currentRecipe, string refinementPrompt, string? customModel = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity and API key validity against the Gemini endpoint.
    /// </summary>
    Task<bool> TestConnectionAsync(string? apiKey = null, string? model = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a collection of pre-configured sample prompts for quick-start builds.
    /// </summary>
    List<PresetPrompt> GetPresetPrompts();
}

/// <summary>
/// Represents a pre-configured template prompt shown in the UI.
/// </summary>
public class PresetPrompt
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
