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
/// Generates package plans and human-readable command suggestions, with deterministic offline fallbacks.
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
                Id = "fedora-win11-dev",
                Title = "Fedora (Win11 UI & Dev)",
                Description =
                    "Fedora Linux with Windows 11 style desktop theme, C++, C# (.NET 8), and IDE dependencies",
                Prompt =
                    "Build a Fedora Linux distribution with a modern Windows 11-style desktop theme, full developer toolchain for C++ (gcc, g++, cmake) and C# (.NET 8 SDK), Git, and JetBrains Rider dependencies.",
                Category = "Workstation",
                Icon = "layout"
            },
            new()
            {
                Id = "arch-hyprland",
                Title = "Arch Linux (Hyprland / Wayland)",
                Description =
                    "Bleeding-edge Arch Linux rolling release with Hyprland Wayland compositor, Alacritty, and dev tools",
                Prompt =
                    "Build a modern Arch Linux live distribution with Hyprland Wayland compositor, Waybar, Alacritty terminal, Neovim, Git, zsh, and pipewire audio.",
                Category = "Desktop",
                Icon = "terminal"
            },
            new()
            {
                Id = "ubuntu-server-docker",
                Title = "Ubuntu 24.04 (Server & Docker)",
                Description = "Ubuntu Noble server image with Docker engine, Docker Compose, Portainer, and OpenSSH",
                Prompt =
                    "Create an Ubuntu 24.04 LTS server ISO with Docker CE, docker-compose, OpenSSH server, UFW firewall, fail2ban, and btop monitoring.",
                Category = "Server",
                Icon = "server"
            },
            new()
            {
                Id = "dev-environment",
                Title = "Developer Workstation",
                Description = "Lightweight environment with C, Rust, Git, Neovim, tmux, and CLI utilities",
                Prompt =
                    "Build a lightweight developer workstation based on Alpine Linux with Neovim, Git, Rust, GCC, curl, tmux, zsh, and htop.",
                Category = "Development",
                Icon = "code"
            },
            new()
            {
                Id = "rescue-disk",
                Title = "Rescue & Diagnostics Disk",
                Description = "Tools for disk recovery, partition repair, and hardware diagnostics",
                Prompt =
                    "Create a bootable ISO for disk recovery and PC diagnostics: testdisk, ddrescue, smartmontools, parted, e2fsprogs, pciutils, lshw, memtester, and mc.",
                Category = "Utilities",
                Icon = "shield-alert"
            },
            new()
            {
                Id = "net-analyzer",
                Title = "Network Auditor & Sniffer",
                Description = "Network diagnostics, packet inspection, throughput testing, and security auditing",
                Prompt =
                    "Prepare a network diagnostic distribution for LAN/WLAN analysis: nmap, tcpdump, iperf3, wireshark-cli, curl, mtr, socat, netcat, bind-tools, and iftop.",
                Category = "Network",
                Icon = "activity"
            },
            new()
            {
                Id = "light-desktop",
                Title = "Ultralight Desktop (XFCE)",
                Description = "Minimalist graphical desktop with web browser and file manager",
                Prompt =
                    "Build a lightweight ISO with XFCE graphical desktop, Chromium browser, Thunar file manager, terminal emulator, and mousepad editor.",
                Category = "Desktop",
                Icon = "monitor"
            }
        };
    }

    /// <summary>
    /// Tests connectivity with the Gemini API using the provided or configured credentials.
    /// </summary>
    public async Task<bool> TestConnectionAsync(string? apiKey = null, string? model = null,
        CancellationToken cancellationToken = default)
    {
        var key = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : _settings.ApiKey;
        var selectedModel = !string.IsNullOrWhiteSpace(model) ? model : _settings.Model;

        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var candidateModels = new List<string> { selectedModel };
        if (!candidateModels.Contains("gemini-flash-latest")) candidateModels.Add("gemini-flash-latest");
        if (!candidateModels.Contains("gemini-flash-lite-latest")) candidateModels.Add("gemini-flash-lite-latest");

        foreach (var m in candidateModels)
        {
            try
            {
                var url = $"{_settings.ApiUrl.TrimEnd('/')}/models/{m}:generateContent?key={key}";
                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = "Respond with one word: OK" } }
                        }
                    }
                };

                var jsonContent =
                    new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(url, jsonContent, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed connection test with model {Model}", m);
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves package changes and command suggestions from a user prompt.
    /// </summary>
    public async Task<IsoRecipe> GenerateRecipeAsync(string prompt, string? customModel = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _settings.ApiKey;
        var model = !string.IsNullOrWhiteSpace(customModel) ? customModel : _settings.Model;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("No Gemini API key configured. Generating offline recipe for prompt: {Prompt}", prompt);
            return GenerateOfflineRecipe(prompt);
        }

        var systemPrompt = BuildSystemInstruction();
        var userInstruction =
            $"USER REQUEST:\n{prompt}\n\nReturn package additions/removals and useful command suggestions only, using the specified JSON schema.";

        return await CallGeminiAndParseRecipe(systemPrompt, userInstruction, apiKey, model, cancellationToken);
    }

    /// <summary>
    /// Refines an existing ISO recipe based on iterative user instructions.
    /// </summary>
    public async Task<IsoRecipe> RefineRecipeAsync(IsoRecipe currentRecipe, string refinementPrompt,
        string? customModel = null, CancellationToken cancellationToken = default)
    {
        var apiKey = _settings.ApiKey;
        var model = !string.IsNullOrWhiteSpace(customModel) ? customModel : _settings.Model;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("No Gemini API key configured. Refining recipe offline.");
            return RefineOfflineRecipe(currentRecipe, refinementPrompt);
        }

        var systemPrompt = BuildSystemInstruction();
        var currentJson = JsonSerializer.Serialize(new
        {
            currentRecipe.Packages,
            currentRecipe.PackagesToRemove,
            currentRecipe.SuggestedCommands
        }, JsonOptions);
        var userInstruction =
            $"CURRENT PACKAGE PLAN JSON:\n{currentJson}\n\nUSER MODIFICATION REQUEST:\n{refinementPrompt}\n\nReturn the complete updated package plan only.";

        var updatedRecipe =
            await CallGeminiAndParseRecipe(systemPrompt, userInstruction, apiKey, model, cancellationToken);
        updatedRecipe.Distribution = currentRecipe.Distribution;
        updatedRecipe.Release = currentRecipe.Release;
        updatedRecipe.Arch = currentRecipe.Arch;
        updatedRecipe.IsoLabel = currentRecipe.IsoLabel;
        updatedRecipe.DesktopEnvironment = currentRecipe.DesktopEnvironment;
        updatedRecipe.TargetUseCase = currentRecipe.TargetUseCase;
        return updatedRecipe;
    }

    /// <summary>
    /// Sends an HTTP request to the Gemini API and parses the returned JSON payload into an IsoRecipe instance.
    /// Automatically performs fallback to newer, active models (e.g. gemini-flash-latest, gemini-flash-lite-latest) if the requested model returns 404/429/503.
    /// </summary>
    private async Task<IsoRecipe> CallGeminiAndParseRecipe(string systemPrompt, string userContent, string apiKey,
        string model, CancellationToken cancellationToken)
    {
        var candidateModels = new List<string> { model };
        if (!candidateModels.Contains("gemini-flash-latest")) candidateModels.Add("gemini-flash-latest");
        if (!candidateModels.Contains("gemini-flash-lite-latest")) candidateModels.Add("gemini-flash-lite-latest");
        if (!candidateModels.Contains("gemini-3.1-pro-preview")) candidateModels.Add("gemini-3.1-pro-preview");
        if (!candidateModels.Contains("gemini-3-flash-preview")) candidateModels.Add("gemini-3-flash-preview");

        Exception? lastException = null;

        foreach (var currentModel in candidateModels)
        {
            try
            {
                var url = $"{_settings.ApiUrl.TrimEnd('/')}/models/{currentModel}:generateContent?key={apiKey}";

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

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8,
                    "application/json");
                var response = await _httpClient.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var recipe = ParseRecipeFromGeminiResponse(responseBody);
                    if (currentModel != model)
                    {
                        recipe.Summary = $"[Auto-fallback: used {currentModel} instead of {model}]: " + recipe.Summary;
                    }

                    return recipe;
                }

                var errorResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Gemini API error for model {Model} ({StatusCode}): {Body}. Trying fallback candidate...",
                    currentModel, response.StatusCode, errorResponse);
                lastException =
                    new HttpRequestException(
                        $"Gemini API error for model {currentModel} ({response.StatusCode}): {errorResponse}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Exception calling Gemini model {Model}. Attempting next candidate...",
                    currentModel);
            }
        }

        throw lastException ??
              new InvalidOperationException("Failed to generate recipe with all candidate Gemini models.");
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
    /// Validates the package plan and discards fields outside Gemini's limited role.
    /// </summary>
    private static void SanitizeRecipe(IsoRecipe recipe)
    {
        recipe.Packages ??= [];
        recipe.PackagesToRemove ??= [];
        recipe.SuggestedCommands ??= [];

        static List<string> ValidatePackageList(IEnumerable<string> packages)
        {
            var result = packages
                .Where(package => !string.IsNullOrWhiteSpace(package))
                .Select(package => package.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (result.Count > 100 || result.Any(package =>
                    !Regex.IsMatch(package, @"^[a-zA-Z0-9][a-zA-Z0-9.+-]*$")))
            {
                throw new InvalidOperationException("Gemini returned invalid package names.");
            }

            return result;
        }

        recipe.Packages = ValidatePackageList(recipe.Packages);
        recipe.PackagesToRemove = ValidatePackageList(recipe.PackagesToRemove);
        if (recipe.Packages.Intersect(recipe.PackagesToRemove, StringComparer.OrdinalIgnoreCase).Any())
        {
            throw new InvalidOperationException("Gemini returned a package for both installation and removal.");
        }

        if (recipe.SuggestedCommands.Count > 20 || recipe.SuggestedCommands.Any(command =>
                string.IsNullOrWhiteSpace(command) || command.Length > 300 ||
                command.Any(char.IsControl) || command.Contains("&&", StringComparison.Ordinal) ||
                command.Contains("||", StringComparison.Ordinal) || command.Contains(';') ||
                command.Contains('|') || command.Contains('`') || command.Contains('$') ||
                command.Contains('>') || command.Contains('<')))
        {
            throw new InvalidOperationException("Gemini returned unsafe or invalid command suggestions.");
        }

        recipe.Distribution = "detected";
        recipe.Release = "detected";
        recipe.Arch = "x86_64";
        recipe.IsoLabel = "AI_LINUX";
        recipe.Hostname = "ailinux";
        recipe.DefaultUsername = "user";
        recipe.RootPassword = "root";
        recipe.DefaultUserPassword = "live";
        recipe.EnableSudo = true;
        recipe.DesktopEnvironment = "none";
        recipe.ServicesToEnable = [];
        recipe.CustomCommands = [];
        recipe.KernelParameters = "quiet console=tty0";
        recipe.TargetUseCase = string.Empty;
        recipe.Summary = string.IsNullOrWhiteSpace(recipe.Summary)
            ? "Review the detected package changes and command suggestions before building."
            : recipe.Summary.Trim();
    }

    /// <summary>
    /// Restricts Gemini to package names and non-executable command suggestions.
    /// </summary>
    private static string BuildSystemInstruction()
    {
        return """
               You are a package research assistant for Linux ISO customization. Your only task is to identify package names to install or remove and provide useful command examples related to the requested packages.

               The returned format MUST be a valid JSON object with the following schema:
               {
                 "packages": [
                   "package names to install; names only, no shell syntax"
                 ],
                 "packagesToRemove": [
                   "package names to remove; names only, no shell syntax"
                 ],
                 "suggestedCommands": [
                   "human-readable command examples for the user to review"
                 ],
                 "summary": "Brief package-plan summary"
               }

               RULES:
               1. Do not choose a distribution, release, desktop, ISO label, services, or system configuration.
               2. Return package names only in `packages` and `packagesToRemove`; never put commands or shell syntax in those arrays.
               3. Use the distribution explicitly named by the user to research package names. If none is specified, prefer widely used package names and mark uncertainty in `summary`.
               4. `suggestedCommands` are displayed to the user only. Never include commands that download/execute remote scripts, modify repositories or signing policy, delete arbitrary files, or chain commands.
               5. Use short, single-line command examples only. They are never executed by the application.
               6. Return ONLY the raw JSON object without markdown formatting or surrounding explanations.
               """;
    }

    /// <summary>
    /// Deterministic offline recipe generator used when an API key is absent.
    /// </summary>
    private static IsoRecipe GenerateOfflineRecipe(string prompt)
    {
        var lower = prompt.ToLowerInvariant();

        if (lower.Contains("arch"))
        {
            return new IsoRecipe
            {
                Distribution = "ubuntu",
                Release = "detected",
                Arch = "x86_64",
                Hostname = "linux-ai",
                IsoLabel = "LINUX_CUSTOM",
                RootPassword = "root",
                DefaultUsername = "user",
                DefaultUserPassword = "live",
                EnableSudo = true,
                DesktopEnvironment = lower.Contains("gui") ? "xfce" : "none",
                TargetUseCase = "Linux Workstation",
                Summary = "Customized uploaded live ISO with development tools and utilities.",
                Packages = new List<string>
                {
                    "build-essential", "git", "neovim", "zsh", "htop", "curl", "alacritty", "openssh-server", "sudo"
                },
                ServicesToEnable = new List<string> { "sshd" }
            };
        }

        if (lower.Contains("ubuntu"))
        {
            return new IsoRecipe
            {
                Distribution = "ubuntu",
                Release = "24.04",
                Arch = "x86_64",
                Hostname = "ubuntu-ai",
                IsoLabel = "UBUNTU_CUSTOM",
                RootPassword = "root",
                DefaultUsername = "ubuntu",
                DefaultUserPassword = "live",
                EnableSudo = true,
                DesktopEnvironment = "none",
                TargetUseCase = "Ubuntu Cloud & Container Infrastructure",
                Summary =
                    "Ubuntu 24.04 LTS optimized for cloud instances, Docker container workloads, and modern networking.",
                Packages = new List<string>
                    { "build-essential", "curl", "git", "docker.io", "openssh-server", "htop", "net-tools", "sudo" },
                ServicesToEnable = new List<string> { "docker", "ssh" }
            };
        }

        if (lower.Contains("fedora"))
        {
            return new IsoRecipe
            {
                Distribution = "ubuntu",
                Release = "detected",
                Arch = "x86_64",
                Hostname = "linux-ai",
                IsoLabel = "LINUX_CUSTOM",
                RootPassword = "root",
                DefaultUsername = "user",
                DefaultUserPassword = "live",
                EnableSudo = true,
                DesktopEnvironment = lower.Contains("win") || lower.Contains("gui") ? "gnome" : "none",
                TargetUseCase = "Linux Development Workstation",
                Summary = "Customized uploaded live ISO with development tools and utilities.",
                Packages =
                    new List<string> { "g++", "cmake", "gdb", "git", "curl", "neovim", "htop", "gnome-terminal" },
                ServicesToEnable = new List<string> { "sshd" }
            };
        }

        var recipe = new IsoRecipe
        {
            Distribution = "ubuntu",
            Release = "detected",
            Arch = "x86_64",
            Hostname = "ai-linux",
            IsoLabel = "AI_LINUX",
            RootPassword = "root",
            DefaultUsername = "user",
            DefaultUserPassword = "live",
            EnableSudo = true,
            ServicesToEnable = new List<string> { "networking", "sshd" }
        };

        if (lower.Contains("graphic") || lower.Contains("xfce") || lower.Contains("desktop") || lower.Contains("gui") ||
            lower.Contains("pulpit"))
        {
            recipe.DesktopEnvironment = "xfce";
            recipe.TargetUseCase = "Lightweight XFCE Desktop Environment";
            recipe.Summary =
                "Optimized, lightweight Linux distribution with XFCE graphical desktop, web browser, and desktop utilities.";
            recipe.Packages = new List<string>
                { "bash", "sudo", "curl", "xfce4-terminal", "mousepad", "openssh-server", "htop" };
        }
        else if (lower.Contains("rescue") || lower.Contains("disk") || lower.Contains("recover") ||
                 lower.Contains("diag") || lower.Contains("ratunk"))
        {
            recipe.DesktopEnvironment = "none";
            recipe.TargetUseCase = "System Recovery & Hardware Diagnostics";
            recipe.Summary =
                "Emergency rescue distribution equipped with filesystem recovery, disk partitioning, and hardware diagnostic utilities.";
            recipe.Packages = new List<string>
            {
                "bash", "sudo", "curl", "testdisk", "gddrescue", "smartmontools", "parted", "e2fsprogs", "dosfstools",
                "pciutils", "lshw", "mc", "htop", "openssh-server"
            };
        }
        else if (lower.Contains("net") || lower.Contains("nmap") || lower.Contains("sniff") ||
                 lower.Contains("audit") || lower.Contains("siec"))
        {
            recipe.DesktopEnvironment = "none";
            recipe.TargetUseCase = "Network Analysis & Security Auditing";
            recipe.Summary =
                "Comprehensive suite of networking tools for traffic inspection, port scanning, performance benchmarking, and security checks.";
            recipe.Packages = new List<string>
            {
                "bash", "sudo", "curl", "nmap", "tcpdump", "iperf3", "tshark", "socat", "netcat-openbsd", "dnsutils",
                "iftop", "openssh-server"
            };
        }
        else
        {
            recipe.DesktopEnvironment = "none";
            recipe.TargetUseCase = "Developer Workstation & CLI Environment";
            recipe.Summary =
                $"Custom Linux environment built for: \"{prompt}\" with essential CLI utilities and build toolchains.";
            recipe.Packages = new List<string>
                { "bash", "sudo", "curl", "git", "neovim", "tmux", "htop", "build-essential", "openssh-server" };
        }

        return recipe;
    }

    /// <summary>
    /// Deterministic offline recipe refinement helper.
    /// </summary>
    private static IsoRecipe RefineOfflineRecipe(IsoRecipe recipe, string refinementPrompt)
    {
        recipe.Summary += $" [Updated: {refinementPrompt}]";
        recipe.PackagesToRemove ??= [];
        recipe.SuggestedCommands ??= [];

        var removeMatch = Regex.Match(refinementPrompt,
            @"(?:remove|uninstall|delete|usuń|usun)\s+(.+?)(?=\s+(?:and|or|add|install|dodaj)\b|$)",
            RegexOptions.IgnoreCase);
        var addMatch = Regex.Match(refinementPrompt,
            @"(?:add|install|dodaj)\s+(.+?)(?=\s+(?:and|or|remove|uninstall|delete|usuń|usun)\b|$)",
            RegexOptions.IgnoreCase);
        foreach (var package in ExtractPackageTokens(removeMatch))
        {
            recipe.Packages.RemoveAll(existing => existing.Equals(package, StringComparison.OrdinalIgnoreCase));
            if (!recipe.PackagesToRemove.Contains(package, StringComparer.OrdinalIgnoreCase))
            {
                recipe.PackagesToRemove.Add(package);
            }
        }

        foreach (var package in ExtractPackageTokens(addMatch))
        {
            recipe.PackagesToRemove.RemoveAll(existing => existing.Equals(package, StringComparison.OrdinalIgnoreCase));
            if (!recipe.Packages.Contains(package, StringComparer.OrdinalIgnoreCase))
            {
                recipe.Packages.Add(package);
            }
        }

        return recipe;
    }

    private static IEnumerable<string> ExtractPackageTokens(Match match)
    {
        if (!match.Success)
        {
            return [];
        }

        return Regex.Split(match.Groups[1].Value, @"[\s,]+")
            .Select(token => token.Trim('.', ';', ':', '"', '\'').ToLowerInvariant())
            .Where(token => Regex.IsMatch(token, @"^[a-zA-Z0-9][a-zA-Z0-9.+-]*$") &&
                            token is not ("and" or "or" or "package" or "packages"));
    }
}