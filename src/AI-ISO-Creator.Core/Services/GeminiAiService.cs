using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AI_ISO_Creator.Core.Configuration;
using AI_ISO_Creator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI_ISO_Creator.Core.Services;

/// <summary>
/// Implementation of the Gemini AI integration service using Google Generative Language REST APIs.
/// Includes structured JSON output parsing, conversational recipe refinement, and deterministic offline fallbacks.
/// </summary>
public class GeminiAiService : IGeminiAiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public GeminiAiService(
        HttpClient httpClient,
        IOptions<GeminiSettings> settings,
        ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Returns curated preset prompts covering typical Linux use-cases.
    /// </summary>
    public List<PresetPrompt> GetPresetPrompts()
    {
        return new List<PresetPrompt>
        {
            new()
            {
                Id = "dev-environment",
                Title = "Developer Workstation",
                Description = "Lightweight environment with C, Rust, Git, Neovim, tmux, and CLI utilities",
                Prompt = "Build a lightweight developer workstation based on Alpine Linux with Neovim, Git, Rust, GCC, curl, tmux, zsh, and htop.",
                Category = "Development",
                Icon = "code"
            },
            new()
            {
                Id = "rescue-disk",
                Title = "Rescue & Diagnostics Disk",
                Description = "Tools for disk recovery, partition repair, and hardware diagnostics",
                Prompt = "Create a bootable ISO for disk recovery and PC diagnostics: testdisk, ddrescue, smartmontools, parted, e2fsprogs, pciutils, lshw, memtester, and mc.",
                Category = "Utilities",
                Icon = "shield-alert"
            },
            new()
            {
                Id = "net-analyzer",
                Title = "Network Auditor & Sniffer",
                Description = "Network diagnostics, packet inspection, throughput testing, and security auditing",
                Prompt = "Prepare a network diagnostic distribution for LAN/WLAN analysis: nmap, tcpdump, iperf3, wireshark-cli, curl, mtr, socat, netcat, bind-tools, and iftop.",
                Category = "Network",
                Icon = "activity"
            },
            new()
            {
                Id = "light-desktop",
                Title = "Ultralight Desktop (XFCE)",
                Description = "Minimalist graphical desktop with web browser and file manager",
                Prompt = "Build a lightweight ISO with XFCE graphical desktop, Chromium browser, Thunar file manager, terminal emulator, and mousepad editor.",
                Category = "Desktop",
                Icon = "monitor"
            },
            new()
            {
                Id = "kiosk-display",
                Title = "Digital Signage / Kiosk",
                Description = "Appliance booting directly into fullscreen browser kiosk mode",
                Prompt = "Prepare an autonomous web kiosk that boots directly into full-screen browser mode displaying https://google.com.",
                Category = "Embedded",
                Icon = "tv"
            }
        };
    }

    /// <summary>
    /// Tests connectivity with the Gemini API using the provided or configured credentials.
    /// </summary>
    public async Task<bool> TestConnectionAsync(string? apiKey = null, string? model = null, CancellationToken cancellationToken = default)
    {
        var key = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _settings.ApiKey;
        var selectedModel = !string.IsNullOrWhiteSpace(model) ? model : _settings.Model;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        try
        {
            var url = $"{_settings.ApiUrl.TrimEnd('/')}/models/{selectedModel}:generateContent?key={key}";
            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = "Respond with one word: OK" }
                        }
                    }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to test connection to Gemini API");
            return false;
        }
    }

    /// <summary>
    /// Generates a complete ISO recipe from a user prompt using Gemini AI or fallback heuristics.
    /// </summary>
    public async Task<IsoRecipe> GenerateRecipeAsync(string prompt, string? customModel = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _settings.ApiKey;
        var model = !string.IsNullOrWhiteSpace(customModel) ? customModel : _settings.Model;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("No Gemini API key configured. Generating offline recipe for prompt: {Prompt}", prompt);
            return GenerateOfflineRecipe(prompt);
        }

        var systemPrompt = BuildSystemInstruction();
        var userInstruction = $"USER REQUEST:\n{prompt}\n\nGenerate an optimal, production-ready Linux ISO recipe following the specified JSON schema.";

        return await CallGeminiAndParseRecipe(systemPrompt, userInstruction, apiKey, model, cancellationToken);
    }

    /// <summary>
    /// Refines an existing ISO recipe based on iterative user instructions.
    /// </summary>
    public async Task<IsoRecipe> RefineRecipeAsync(IsoRecipe currentRecipe, string refinementPrompt, string? customModel = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _settings.ApiKey;
        var model = !string.IsNullOrWhiteSpace(customModel) ? customModel : _settings.Model;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("No Gemini API key configured. Refining recipe offline.");
            return RefineOfflineRecipe(currentRecipe, refinementPrompt);
        }

        var systemPrompt = BuildSystemInstruction();
        var currentJson = JsonSerializer.Serialize(currentRecipe, JsonOptions);
        var userInstruction = $"CURRENT RECIPE JSON:\n{currentJson}\n\nUSER MODIFICATION REQUEST:\n{refinementPrompt}\n\nUpdate the recipe accordingly and return the complete updated JSON object adhering strictly to the schema.";

        return await CallGeminiAndParseRecipe(systemPrompt, userInstruction, apiKey, model, cancellationToken);
    }

    /// <summary>
    /// Sends an HTTP request to the Gemini API and parses the returned JSON payload into an IsoRecipe instance.
    /// </summary>
    private async Task<IsoRecipe> CallGeminiAndParseRecipe(string systemPrompt, string userContent, string apiKey, string model, CancellationToken cancellationToken)
    {
        var url = $"{_settings.ApiUrl.TrimEnd('/')}/models/{model}:generateContent?key={apiKey}";

        var requestBody = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userContent } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API error ({StatusCode}): {Body}", response.StatusCode, errorResponse);
            throw new HttpRequestException($"Gemini API returned error {response.StatusCode}: {errorResponse}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseRecipeFromGeminiResponse(responseBody);
    }

    /// <summary>
    /// Extracts text from the Gemini response structure and deserializes the JSON schema into an IsoRecipe.
    /// </summary>
    private IsoRecipe ParseRecipeFromGeminiResponse(string geminiResponseJson)
    {
        try
        {
            var node = JsonNode.Parse(geminiResponseJson);
            var candidates = node?["candidates"]?.AsArray();
            var text = candidates?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Gemini API returned empty text content.");
            }

            var cleanJson = ExtractJsonContent(text);
            var recipe = JsonSerializer.Deserialize<IsoRecipe>(cleanJson, JsonOptions);

            if (recipe == null)
            {
                throw new InvalidOperationException("Failed to deserialize IsoRecipe object from model response.");
            }

            SanitizeRecipe(recipe);
            return recipe;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse JSON response from Gemini: {RawResponse}", geminiResponseJson);
            throw;
        }
    }

    /// <summary>
    /// Strips markdown fences if present to ensure pure JSON parsing.
    /// </summary>
    private static string ExtractJsonContent(string text)
    {
        text = text.Trim();

        var match = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Multiline);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        var startIdx = text.IndexOf('{');
        var endIdx = text.LastIndexOf('}');
        if (startIdx >= 0 && endIdx > startIdx)
        {
            return text.Substring(startIdx, endIdx - startIdx + 1);
        }

        return text;
    }

    /// <summary>
    /// Validates and provides fallback defaults for missing recipe fields.
    /// </summary>
    private static void SanitizeRecipe(IsoRecipe recipe)
    {
        recipe.Distribution = string.IsNullOrWhiteSpace(recipe.Distribution) ? "alpine" : recipe.Distribution.ToLowerInvariant();
        recipe.Release = string.IsNullOrWhiteSpace(recipe.Release) ? "3.20" : recipe.Release;
        recipe.Arch = string.IsNullOrWhiteSpace(recipe.Arch) ? "x86_64" : recipe.Arch;
        recipe.Hostname = string.IsNullOrWhiteSpace(recipe.Hostname) ? "ailinux" : Regex.Replace(recipe.Hostname, @"[^a-zA-Z0-9\-]", "");
        recipe.IsoLabel = string.IsNullOrWhiteSpace(recipe.IsoLabel) ? "AI_LINUX" : Regex.Replace(recipe.IsoLabel, @"[^a-zA-Z0-9_\-]", "");
        recipe.DefaultUsername = string.IsNullOrWhiteSpace(recipe.DefaultUsername) ? "user" : recipe.DefaultUsername.ToLowerInvariant();
        recipe.DefaultUserPassword = string.IsNullOrWhiteSpace(recipe.DefaultUserPassword) ? "live" : recipe.DefaultUserPassword;
        recipe.RootPassword = string.IsNullOrWhiteSpace(recipe.RootPassword) ? "root" : recipe.RootPassword;
        recipe.DesktopEnvironment = string.IsNullOrWhiteSpace(recipe.DesktopEnvironment) ? "none" : recipe.DesktopEnvironment.ToLowerInvariant();

        recipe.Packages = recipe.Packages.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        recipe.ServicesToEnable = recipe.ServicesToEnable.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        recipe.CustomCommands = recipe.CustomCommands.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();

        if (string.IsNullOrWhiteSpace(recipe.Summary))
        {
            recipe.Summary = $"Custom {recipe.Distribution} ({recipe.Release}) distribution with packages: {string.Join(", ", recipe.Packages.Take(5))}...";
        }
    }

    /// <summary>
    /// Returns the system instructions guiding the AI model to output strictly formatted JSON.
    /// </summary>
    private static string BuildSystemInstruction()
    {
        return """
You are an expert Linux distribution architect and specialist in building live bootable Linux ISO images (Alpine Linux and Debian/Ubuntu).
Your mission is to transform user intent expressed in natural language into a precise, cohesive machine-readable Linux ISO build recipe (IsoRecipe).

The returned format MUST be a valid JSON object with the following schema:
{
  "distribution": "alpine" or "debian" (Alpine is recommended for speed and compact size ~100MB),
  "release": "3.20" for Alpine or "bookworm" for Debian,
  "arch": "x86_64",
  "isoLabel": "SHORT_UPPERCASE_LABEL_NO_SPACES",
  "hostname": "system-hostname",
  "rootPassword": "root",
  "defaultUsername": "user",
  "defaultUserPassword": "live",
  "enableSudo": true,
  "desktopEnvironment": "none", "xfce", or "i3",
  "packages": [
    "list", "of", "packages", "compatible", "with", "the", "chosen", "distro"
  ],
  "servicesToEnable": [
    "networking", "sshd"
  ],
  "customCommands": [
    "optional", "shell", "commands", "for", "chroot"
  ],
  "kernelParameters": "quiet console=tty0",
  "summary": "Clear, professional technical rationale describing what this image contains and why.",
  "targetUseCase": "Short summary of the primary use case (e.g., 'Network Auditor')"
}

RULES:
1. Ensure package names in `packages` match the selected distribution (e.g., for Alpine use `openssh`, `util-linux`, `bash`, `neovim`, `build-base`; for Debian use `build-essential`, `openssh-server`, etc.).
2. Always ensure core essentials are included (e.g. bash, curl, sudo/doas).
3. If the user requests a graphical desktop, include proper X11/Wayland dependencies and the chosen window manager/desktop environment (e.g. xfce4, lightdm).
4. Return ONLY the raw JSON object without markdown formatting or surrounding explanations.
""";
    }

    /// <summary>
    /// Deterministic offline recipe generator used when an API key is absent.
    /// </summary>
    private static IsoRecipe GenerateOfflineRecipe(string prompt)
    {
        var lower = prompt.ToLowerInvariant();
        var recipe = new IsoRecipe
        {
            Distribution = "alpine",
            Release = "3.20",
            Arch = "x86_64",
            Hostname = "ai-linux",
            IsoLabel = "AI_LINUX",
            RootPassword = "root",
            DefaultUsername = "user",
            DefaultUserPassword = "live",
            EnableSudo = true,
            ServicesToEnable = new List<string> { "networking", "sshd" }
        };

        if (lower.Contains("graphic") || lower.Contains("xfce") || lower.Contains("desktop") || lower.Contains("gui") || lower.Contains("pulpit"))
        {
            recipe.DesktopEnvironment = "xfce";
            recipe.TargetUseCase = "Lightweight XFCE Desktop Environment";
            recipe.Summary = "Optimized, lightweight Linux distribution with XFCE graphical desktop, web browser, and desktop utilities.";
            recipe.Packages = new List<string> { "bash", "sudo", "curl", "xfce4", "xfce4-terminal", "chromium", "mousepad", "openssh", "htop" };
        }
        else if (lower.Contains("rescue") || lower.Contains("disk") || lower.Contains("recover") || lower.Contains("diag") || lower.Contains("ratunk"))
        {
            recipe.DesktopEnvironment = "none";
            recipe.TargetUseCase = "System Recovery & Hardware Diagnostics";
            recipe.Summary = "Emergency rescue distribution equipped with filesystem recovery, disk partitioning, and hardware diagnostic utilities.";
            recipe.Packages = new List<string> { "bash", "sudo", "curl", "testdisk", "smartmontools", "parted", "e2fsprogs", "dosfstools", "pciutils", "lshw", "mc", "htop", "openssh" };
        }
        else if (lower.Contains("net") || lower.Contains("nmap") || lower.Contains("sniff") || lower.Contains("audit") || lower.Contains("siec"))
        {
            recipe.DesktopEnvironment = "none";
            recipe.TargetUseCase = "Network Analysis & Security Auditing";
            recipe.Summary = "Comprehensive suite of networking tools for traffic inspection, port scanning, performance benchmarking, and security checks.";
            recipe.Packages = new List<string> { "bash", "sudo", "curl", "nmap", "tcpdump", "iperf3", "wireshark-cli", "socat", "netcat-openbsd", "bind-tools", "iftop", "openssh" };
        }
        else
        {
            recipe.DesktopEnvironment = "none";
            recipe.TargetUseCase = "Developer Workstation & CLI Environment";
            recipe.Summary = $"Custom Linux environment built for: \"{prompt}\" with essential CLI utilities and build toolchains.";
            recipe.Packages = new List<string> { "bash", "sudo", "curl", "git", "neovim", "tmux", "htop", "build-base", "openssh" };
        }

        return recipe;
    }

    /// <summary>
    /// Deterministic offline recipe refinement helper.
    /// </summary>
    private static IsoRecipe RefineOfflineRecipe(IsoRecipe recipe, string refinementPrompt)
    {
        var lower = refinementPrompt.ToLowerInvariant();
        recipe.Summary += $" [Updated: {refinementPrompt}]";

        var words = refinementPrompt.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            var clean = word.Trim().ToLowerInvariant();
            if (clean.Length > 2 && !clean.StartsWith("add") && !clean.StartsWith("remove") && !clean.StartsWith("dodaj") && !clean.StartsWith("usun"))
            {
                if (lower.Contains("remove " + clean) || lower.Contains("usun " + clean))
                {
                    recipe.Packages.RemoveAll(p => p.Equals(clean, StringComparison.OrdinalIgnoreCase));
                }
                else if (lower.Contains("add " + clean) || lower.Contains("dodaj " + clean) || lower.Contains("install"))
                {
                    if (!recipe.Packages.Contains(clean))
                    {
                        recipe.Packages.Add(clean);
                    }
                }
            }
        }

        return recipe;
    }
}
