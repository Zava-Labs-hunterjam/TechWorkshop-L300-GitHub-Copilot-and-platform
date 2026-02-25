using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ZavaStorefront.Services;

public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;

    public ChatService(HttpClient httpClient, IConfiguration configuration, ILogger<ChatService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> SendMessageAsync(string userMessage)
    {
        var endpoint = _configuration["AiFoundry:Endpoint"]
            ?? throw new InvalidOperationException("AiFoundry:Endpoint is not configured.");
        var apiKey = _configuration["AiFoundry:ApiKey"]
            ?? throw new InvalidOperationException("AiFoundry:ApiKey is not configured.");

        var deploymentName = "Phi-4-mini-instruct";
        var requestUrl = $"{endpoint.TrimEnd('/')}/openai/deployments/{deploymentName}/chat/completions?api-version=2024-04-01-preview";

        var requestBody = new
        {
            messages = new[]
            {
                new { role = "system", content = "You are a helpful shopping assistant for Zava Storefront." },
                new { role = "user", content = userMessage }
            },
            max_tokens = 800,
            temperature = 0.7
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);

        _logger.LogInformation("Sending chat request to Phi-4-mini-instruct deployment");

        var response = await _httpClient.PostAsync(requestUrl, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("AI Foundry API returned {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new HttpRequestException($"AI service returned {response.StatusCode}.");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var reply = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return reply ?? "No response received.";
    }
}
