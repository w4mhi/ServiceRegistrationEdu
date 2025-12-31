using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Omni.ServiceRegistry.Services.LLM;

/// <summary>
/// HTTP client wrapper for Ollama API integration
/// </summary>
public class OllamaService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IConfiguration configuration;
    private readonly ILogger<OllamaService>? logger;
    private readonly string baseUrl;
    private readonly string model;
    private readonly int maxTokens;
    private readonly double temperature;

    public OllamaService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<OllamaService>? logger = null)
    {
        this.httpClientFactory = httpClientFactory;
        this.configuration = configuration;
        this.logger = logger;

        baseUrl = configuration.GetValue<string>("HealthInsights:OllamaBaseUrl", "http://localhost:11434");
        model = configuration.GetValue<string>("HealthInsights:OllamaModel", "phi4");
        maxTokens = configuration.GetValue<int>("HealthInsights:MaxTokens", 4500);
        temperature = configuration.GetValue<double>("HealthInsights:Temperature", 0.7);
    }

    /// <summary>
    /// Generate text completion using Ollama API
    /// </summary>
    public async Task<OllamaResponse> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.UtcNow;

        HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        httpClient.Timeout = TimeSpan.FromMinutes(5);

        try
        {
            OllamaRequest request = new OllamaRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Options = new OllamaOptions
                {
                    Temperature = temperature,
                    NumPredict = maxTokens
                }
            };

            string jsonRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            HttpContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            logger?.LogInformation("Sending request to Ollama API: model={Model}, prompt length={PromptLength}", 
                model, prompt.Length);

            HttpResponseMessage response = await httpClient.PostAsync("/api/generate", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                logger?.LogError("Ollama API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Ollama API returned {response.StatusCode}: {errorContent}");
            }

            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            OllamaApiResponse? apiResponse = JsonSerializer.Deserialize<OllamaApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (apiResponse == null || string.IsNullOrWhiteSpace(apiResponse.Response))
            {
                throw new InvalidOperationException("Ollama API returned empty response");
            }

            int processingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;

            logger?.LogInformation("Ollama API success: tokens={Tokens}, time={TimeMs}ms", 
                apiResponse.EvalCount ?? 0, processingTimeMs);

            return new OllamaResponse
            {
                Response = apiResponse.Response.Trim(),
                TokensUsed = apiResponse.EvalCount ?? 0,
                ProcessingTimeMs = processingTimeMs,
                Model = apiResponse.Model ?? model
            };
        }
        catch (TaskCanceledException ex)
        {
            logger?.LogWarning(ex, "Ollama API request timed out");
            throw new TimeoutException("Ollama API request timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            logger?.LogError(ex, "Ollama API request failed");
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error calling Ollama API");
            throw;
        }
    }

    /// <summary>
    /// Check if Ollama service is available
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(baseUrl);
        
        try
        {
            HttpResponseMessage response = await httpClient.GetAsync("/api/tags", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Request payload for Ollama API
/// </summary>
internal class OllamaRequest
{
    public string Model { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool Stream { get; set; } = false;
    public OllamaOptions Options { get; set; } = new OllamaOptions();
}

/// <summary>
/// Options for Ollama generation
/// </summary>
internal class OllamaOptions
{
    public double Temperature { get; set; } = 0.7;
    public int NumPredict { get; set; } = 4500;
}

/// <summary>
/// Response from Ollama API
/// </summary>
internal class OllamaApiResponse
{
    public string? Model { get; set; }
    public string? Response { get; set; }
    public int? EvalCount { get; set; }
}

/// <summary>
/// Parsed Ollama response with metadata
/// </summary>
public class OllamaResponse
{
    public string Response { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public int ProcessingTimeMs { get; set; }
    public string Model { get; set; } = string.Empty;
}
