using AI_ISO_Creator.Core.Configuration;
using AI_ISO_Creator.Core.Models;
using AI_ISO_Creator.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AI_ISO_Creator.Tests;

/// <summary>
/// Unit tests verifying Gemini AI service operations, template prompts, and offline fallbacks.
/// </summary>
public class GeminiAiServiceTests
{
    private readonly IGeminiAiService _service;

    public GeminiAiServiceTests()
    {
        var settings = Options.Create(new GeminiSettings
        {
            ApiKey = "", // Empty to trigger offline deterministic fallback heuristics
            Model = "gemini-flash-latest"
        });

        _service = new GeminiAiService(
            new HttpClient(),
            settings,
            NullLogger<GeminiAiService>.Instance);
    }

    [Fact]
    public void GetPresetPrompts_ReturnsNonEmptyList()
    {
        // Act
        var presets = _service.GetPresetPrompts();

        // Assert
        Assert.NotNull(presets);
        Assert.NotEmpty(presets);
        Assert.Contains(presets, p => p.Id == "rescue-disk");
        Assert.Contains(presets, p => p.Id == "dev-environment");
    }

    [Fact]
    public async Task GenerateRecipeAsync_WhenNoApiKey_ReturnsIntelligentFallbackRecipe()
    {
        // Arrange
        var prompt = "I need a rescue system for disk recovery and hardware diagnostics";

        // Act
        var recipe = await _service.GenerateRecipeAsync(prompt);

        // Assert
        Assert.NotNull(recipe);
        Assert.Equal("ubuntu", recipe.Distribution);
        Assert.Contains("testdisk", recipe.Packages);
        Assert.Contains("smartmontools", recipe.Packages);
        Assert.Equal("none", recipe.DesktopEnvironment);
    }

    [Fact]
    public async Task GenerateRecipeAsync_DesktopPrompt_IncludesDesktopEnvironment()
    {
        // Arrange
        var prompt = "Build a lightweight xfce desktop with browser";

        // Act
        var recipe = await _service.GenerateRecipeAsync(prompt);

        // Assert
        Assert.NotNull(recipe);
        Assert.Equal("xfce", recipe.DesktopEnvironment);
        Assert.Contains("xfce4-terminal", recipe.Packages);
    }

    [Fact]
    public async Task RefineRecipeAsync_AddsOrRemovesPackages()
    {
        // Arrange
        var initialRecipe = new IsoRecipe
        {
            Distribution = "ubuntu",
            Packages = new List<string> { "bash", "curl", "htop" }
        };

        // Act
        var refined = await _service.RefineRecipeAsync(initialRecipe, "add nmap and remove htop");

        // Assert
        Assert.NotNull(refined);
        Assert.Contains("nmap", refined.Packages);
        Assert.DoesNotContain("htop", refined.Packages);
    }
}
