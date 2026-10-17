using AI_ISO_Creator.Core.Configuration;
using AI_ISO_Creator.Core.Models;
using AI_ISO_Creator.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;
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
        Assert.Contains("htop", refined.PackagesToRemove);
    }

    [Fact]
    public async Task GenerateRecipeAsync_GeminiCanOnlySetPackagePlan()
    {
        var response = JsonSerializer.Serialize(new
        {
            candidates = new[]
            {
                new
                {
                    content = new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = """
                                       {"packages":["vlc"],"packagesToRemove":["nano"],"suggestedCommands":["apt-get install vlc"],"distribution":"alpine","desktopEnvironment":"gnome","isoLabel":"OVERRIDE"}
                                       """
                            }
                        }
                    }
                }
            }
        });
        var handler = new StaticGeminiHandler(response);
        var service = new GeminiAiService(
            new HttpClient(handler),
            Options.Create(new GeminiSettings
            {
                ApiKey = "test-key",
                ApiUrl = "https://example.invalid/v1beta",
                Model = "gemini-flash-latest"
            }),
            NullLogger<GeminiAiService>.Instance);

        var recipe = await service.GenerateRecipeAsync("Install VLC and remove nano");

        Assert.Equal(new[] { "vlc" }, recipe.Packages);
        Assert.Equal(new[] { "nano" }, recipe.PackagesToRemove);
        Assert.Equal(new[] { "apt-get install vlc" }, recipe.SuggestedCommands);
        Assert.Equal("detected", recipe.Distribution);
        Assert.Equal("none", recipe.DesktopEnvironment);
        Assert.Equal("AI_LINUX", recipe.IsoLabel);
        Assert.Contains("package research assistant", handler.RequestBody, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StaticGeminiHandler(string responseBody) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
