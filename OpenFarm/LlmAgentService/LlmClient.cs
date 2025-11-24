using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmAgentService;

public class LlmClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmClient> _logger;
    private readonly string _model;

    public LlmClient(HttpClient httpClient, IConfiguration configuration, ILogger<LlmClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = configuration["LLM_MODEL"] ?? "llama3";
    }

    public async Task EnsureModelLoadedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            response.EnsureSuccessStatusCode();
            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(cancellationToken: cancellationToken);

            if (tags?.Models.Any(m => m.Name.StartsWith(_model)) == true)
            {
                _logger.LogInformation("Model {Model} is already available.", _model);
                return;
            }

            _logger.LogInformation("Model {Model} not found. Pulling...", _model);
            var pullResponse = await _httpClient.PostAsJsonAsync("/api/pull", new { name = _model }, cancellationToken);
            pullResponse.EnsureSuccessStatusCode();
            
            // Pulling can take a while, so we just wait for the request to complete. 
            // In a real scenario, we might want to stream the response to show progress.
            _logger.LogInformation("Model {Model} pulled successfully.", _model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring model is loaded.");
        }
    }

    public async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/embeddings", new { model = _model, prompt = text }, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(cancellationToken: cancellationToken);
            return result?.Embedding;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embedding.");
            return null;
        }
    }

    public async Task<string?> GenerateCompletionAsync(string prompt, string systemPrompt, CancellationToken cancellationToken)
    {
        try
        {
            var request = new
            {
                model = _model,
                prompt = prompt,
                system = systemPrompt,
                stream = false
            };

            var response = await _httpClient.PostAsJsonAsync("/api/generate", request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return result?.Response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion.");
            return null;
        }
    }

    public async Task<int?> GetEmbeddingDimensionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Generate a test embedding to determine dimensions
            var testEmbedding = await GenerateEmbeddingAsync("test", cancellationToken);
            if (testEmbedding != null)
            {
                _logger.LogInformation("Model {Model} generates embeddings with {Dimensions} dimensions.", _model, testEmbedding.Length);
                return testEmbedding.Length;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting embedding dimensions.");
            return null;
        }
    }

    private class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModel> Models { get; set; } = new();
    }

    private class OllamaModel
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    private class OllamaEmbeddingResponse
    {
        [JsonPropertyName("embedding")]
        public float[]? Embedding { get; set; }
    }

    private class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string? Response { get; set; }
    }
}
