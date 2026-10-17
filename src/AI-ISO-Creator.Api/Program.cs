using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using AI_ISO_Creator.Api.Services;
using AI_ISO_Creator.Core.Configuration;
using AI_ISO_Creator.Core.Models;
using AI_ISO_Creator.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
const long MaxSourceIsoSizeBytes = 16L * 1024 * 1024 * 1024;
const long MaxUploadRequestSizeBytes = MaxSourceIsoSizeBytes + 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxUploadRequestSizeBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = MaxSourceIsoSizeBytes);

// Service dependencies registration
builder.Services.AddSingleton<AppSettingsService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<IIsoBuilderService, ContainerBuilderService>();

builder.Services.AddTransient<IGeminiAiService>(sp =>
{
    var appSettings = sp.GetRequiredService<AppSettingsService>();
    var settings = appSettings.GetSettings();
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var logger = sp.GetRequiredService<ILogger<GeminiAiService>>();
    return new GeminiAiService(httpClient, Options.Create(settings), logger);
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// System health and runtime status endpoint
app.MapGet("/api/status", (AppSettingsService settingsSvc, IIsoBuilderService builderSvc) =>
{
    var cfg = settingsSvc.GetSettings();
    return Results.Ok(new
    {
        HasApiKey = !string.IsNullOrWhiteSpace(cfg.ApiKey),
        SelectedModel = cfg.Model,
        IsPodmanAvailable = builderSvc.IsPodmanAvailable(),
        IsQemuAvailable = builderSvc.IsQemuAvailable()
    });
});

// Configuration endpoints
app.MapGet("/api/config", (AppSettingsService settingsSvc) =>
{
    var cfg = settingsSvc.GetSettings();
    var maskedKey = string.IsNullOrWhiteSpace(cfg.ApiKey) 
        ? "" 
        : (cfg.ApiKey.Length > 8 ? cfg.ApiKey[..4] + "..." + cfg.ApiKey[^4..] : "********");

    return Results.Ok(new
    {
        ApiKeyMasked = maskedKey,
        HasApiKey = !string.IsNullOrWhiteSpace(cfg.ApiKey),
        Model = cfg.Model,
        AvailableModels = new[]
        {
            "gemini-flash-latest",
            "gemini-flash-lite-latest",
            "gemini-3-flash-preview",
            "gemini-3.1-pro-preview",
            "gemini-3.1-flash-lite-preview",
            "gemini-pro-latest"
        }
    });
});

app.MapPost("/api/config", (AppSettingsService settingsSvc, [FromBody] UpdateConfigRequest request) =>
{
    settingsSvc.UpdateSettings(request.ApiKey, request.Model);
    return Results.Ok(new { message = "Configuration updated successfully." });
});

app.MapPost("/api/config/test", async (IGeminiAiService aiService, [FromBody] TestConfigRequest? request) =>
{
    var success = await aiService.TestConnectionAsync(request?.ApiKey, request?.Model);
    return Results.Ok(new
    {
        success,
        message = success 
            ? "Connection to Gemini API succeeded!" 
            : "Failed to connect to Gemini API. Verify your API key or network connection."
    });
});

// Gemini AI generation and refinement endpoints
app.MapGet("/api/ai/presets", (IGeminiAiService aiService) =>
{
    return Results.Ok(aiService.GetPresetPrompts());
});

app.MapPost("/api/ai/generate", async (IGeminiAiService aiService, [FromBody] GenerateRecipeRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { error = "Prompt cannot be empty." });
    }

    try
    {
        var recipe = await aiService.GenerateRecipeAsync(request.Prompt, request.Model);
        return Results.Ok(recipe);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapPost("/api/ai/refine", async (IGeminiAiService aiService, [FromBody] RefineRecipeRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.RefinementPrompt) || request.CurrentRecipe == null)
    {
        return Results.BadRequest(new { error = "Current recipe and refinement instructions are required." });
    }

    try
    {
        var refined = await aiService.RefineRecipeAsync(request.CurrentRecipe, request.RefinementPrompt, request.Model);
        return Results.Ok(refined);
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

// ISO build engine endpoints
app.MapPost("/api/build", async (HttpRequest request, IIsoBuilderService builderSvc, CancellationToken ct) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Upload a source ISO using multipart form data." });
    }

    try
    {
        var form = await request.ReadFormAsync(ct);
        var sourceIso = form.Files.GetFile("sourceIso");
        if (sourceIso == null || sourceIso.Length == 0)
        {
            return Results.BadRequest(new { error = "Choose a non-empty Linux live ISO file." });
        }

        if (sourceIso.Length > MaxSourceIsoSizeBytes)
        {
            return Results.BadRequest(new { error = $"The ISO exceeds the maximum size of {MaxSourceIsoSizeBytes / (1024 * 1024 * 1024)} GiB." });
        }

        if (!Path.GetExtension(sourceIso.FileName).Equals(".iso", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "The uploaded file must have an .iso extension." });
        }

        if (sourceIso.Length < 32774)
        {
            return Results.BadRequest(new { error = "The uploaded file is too small to be a valid ISO image." });
        }

        await using (var validationStream = sourceIso.OpenReadStream())
        {
            validationStream.Position = 32769;
            var volumeDescriptorId = new byte[5];
            await validationStream.ReadExactlyAsync(volumeDescriptorId, ct);
            if (!volumeDescriptorId.SequenceEqual(new byte[] { 67, 68, 48, 48, 49 }))
            {
                return Results.BadRequest(new { error = "The file does not contain an ISO-9660 volume descriptor." });
            }
        }

        var recipeJson = form["recipe"].ToString();
        if (string.IsNullOrWhiteSpace(recipeJson))
        {
            return Results.BadRequest(new { error = "Build configuration is missing." });
        }

        var recipe = JsonSerializer.Deserialize<IsoRecipe>(recipeJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (recipe == null)
        {
            return Results.BadRequest(new { error = "Build configuration is invalid." });
        }

        await using var sourceStream = sourceIso.OpenReadStream();
        var job = await builderSvc.StartBuildAsync(recipe, sourceStream, sourceIso.FileName, ct);
        return Results.Ok(job);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidDataException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Build configuration must be valid JSON." });
    }
    catch (Exception ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: 500);
    }
});

app.MapGet("/api/builds", (IIsoBuilderService builderSvc) =>
{
    return Results.Ok(builderSvc.GetAllJobs());
});

app.MapGet("/api/build/{id}", (IIsoBuilderService builderSvc, string id) =>
{
    var job = builderSvc.GetJob(id);
    return job != null ? Results.Ok(job) : Results.NotFound(new { error = "Build task not found." });
});

app.MapGet("/api/build/{id}/logs", (IIsoBuilderService builderSvc, string id) =>
{
    var job = builderSvc.GetJob(id);
    return job != null ? Results.Ok(job.Logs) : Results.NotFound();
});

// Real-time log and progress streaming via Server-Sent Events (SSE)
app.MapGet("/api/build/{id}/stream", async (HttpContext ctx, IIsoBuilderService builderSvc, string id, CancellationToken ct) =>
{
    var job = builderSvc.GetJob(id);
    if (job == null)
    {
        ctx.Response.StatusCode = 404;
        return;
    }

    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("Connection", "keep-alive");

    var channel = System.Threading.Channels.Channel.CreateUnbounded<string>();

    void OnLog(string jId, BuildLogEntry log)
    {
        if (jId == id)
        {
            var json = JsonSerializer.Serialize(new { type = "log", data = log });
            channel.Writer.TryWrite(json);
        }
    }

    void OnUpdate(string jId, BuildJob updatedJob)
    {
        if (jId == id)
        {
            var json = JsonSerializer.Serialize(new
            {
                type = "job",
                data = new
                {
                    status = updatedJob.Status.ToString(),
                    statusMessage = updatedJob.StatusMessage,
                    progress = updatedJob.ProgressPercent,
                    outputFileName = updatedJob.OutputIsoFileName,
                    outputSize = updatedJob.OutputIsoSize,
                    sha256 = updatedJob.OutputIsoSha256,
                    finishedAt = updatedJob.FinishedAt
                }
            });
            channel.Writer.TryWrite(json);
        }
    }

    builderSvc.OnLogReceived += OnLog;
    builderSvc.OnJobUpdated += OnUpdate;

    try
    {
        // Push initial state
        var initialJson = JsonSerializer.Serialize(new
        {
            type = "job",
            data = new
            {
                status = job.Status.ToString(),
                statusMessage = job.StatusMessage,
                progress = job.ProgressPercent,
                outputFileName = job.OutputIsoFileName,
                outputSize = job.OutputIsoSize,
                sha256 = job.OutputIsoSha256,
                finishedAt = job.FinishedAt
            }
        });
        await ctx.Response.WriteAsync($"data: {initialJson}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            var item = await channel.Reader.ReadAsync(ct);
            await ctx.Response.WriteAsync($"data: {item}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        builderSvc.OnLogReceived -= OnLog;
        builderSvc.OnJobUpdated -= OnUpdate;
    }
});

// Download ISO artifact
app.MapGet("/api/build/{id}/download", (IIsoBuilderService builderSvc, string id) =>
{
    var job = builderSvc.GetJob(id);
    if (job == null || string.IsNullOrWhiteSpace(job.OutputIsoPath) || !File.Exists(job.OutputIsoPath))
    {
        return Results.NotFound(new { error = "ISO file has not been created yet or was removed." });
    }

    var fileName = job.OutputIsoFileName ?? $"{job.Recipe.IsoLabel}.iso";
    return Results.File(job.OutputIsoPath, "application/x-iso9660-image", fileName);
});

// Cancel active build task
app.MapPost("/api/build/{id}/cancel", (IIsoBuilderService builderSvc, string id) =>
{
    var result = builderSvc.CancelJob(id);
    return Results.Ok(new { success = result });
});

// Delete build job and workspace directory
app.MapDelete("/api/build/{id}", (IIsoBuilderService builderSvc, string id) =>
{
    var result = builderSvc.DeleteJob(id);
    return Results.Ok(new { success = result });
});

// Launch completed ISO in QEMU virtual machine
app.MapPost("/api/build/{id}/test-qemu", async (IIsoBuilderService builderSvc, string id) =>
{
    var success = await builderSvc.LaunchInQemuAsync(id);
    return Results.Ok(new
    {
        success,
        message = success 
            ? "Successfully started QEMU virtual machine with the ISO image!" 
            : "Failed to launch QEMU. Ensure the build has completed and QEMU is installed on the host."
    });
});

// Optional desktop browser auto-open after server startup
_ = Task.Run(async () =>
{
    await Task.Delay(1000);
    try
    {
        var url = "http://localhost:5000";
        if (args.Contains("--open-browser") || args.Contains("--gui"))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-open",
                Arguments = url,
                UseShellExecute = true
            });
        }
    }
    catch { /* Ignore browser open failures */ }
});

app.Run();

// Request DTO records
public record UpdateConfigRequest(string? ApiKey, string? Model);
public record TestConfigRequest(string? ApiKey, string? Model);
public record GenerateRecipeRequest(string Prompt, string? Model);
public record RefineRecipeRequest(IsoRecipe CurrentRecipe, string RefinementPrompt, string? Model);
