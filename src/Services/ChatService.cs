using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.AI.ContentSafety;
using Azure.Core;
using Azure.Identity;

namespace ZavaStorefront.Services;

public class ChatService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly TokenCredential _credential;
    private readonly ContentSafetyClient _contentSafetyClient;

    public ChatService(HttpClient httpClient, IConfiguration configuration, ILogger<ChatService> logger, TokenCredential credential, ContentSafetyClient contentSafetyClient)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _credential = credential;
        _contentSafetyClient = contentSafetyClient;
    }

    public async Task<(bool IsSafe, string? Reason)> EvaluateContentSafetyAsync(string text)
    {
        // Check harm categories: Violence, Sexual, Hate, SelfHarm
        var options = new AnalyzeTextOptions(text);
        var response = await _contentSafetyClient.AnalyzeTextAsync(options);

        foreach (var category in response.Value.CategoriesAnalysis)
        {
            if (category.Severity >= 2)
            {
                _logger.LogWarning("Content Safety flagged category {Category} with severity {Severity}",
                    category.Category, category.Severity);
                return (false, $"{category.Category} (severity {category.Severity})");
            }
        }

        // Check for jailbreak via Prompt Shields REST API
        if (await CheckJailbreakAsync(text))
        {
            _logger.LogWarning("Content Safety: jailbreak attempt detected");
            return (false, "Jailbreak attempt detected");
        }

        _logger.LogInformation("Content Safety: user message evaluated as safe");
        return (true, null);
    }

    private async Task<bool> CheckJailbreakAsync(string text)
    {
        var endpoint = _configuration["AiFoundry:Endpoint"]?.TrimEnd('/');
        var requestUrl = $"{endpoint}/contentsafety/text:shieldPrompt?api-version=2024-09-01";

        var requestBody = new { userPrompt = text, documents = Array.Empty<string>() };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var tokenResult = await _credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
            CancellationToken.None);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Jailbreak shield returned {StatusCode}, skipping check", response.StatusCode);
            return false;
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseBody);

        if (doc.RootElement.TryGetProperty("userPromptAnalysis", out var analysis) &&
            analysis.TryGetProperty("attackDetected", out var attackDetected) &&
            attackDetected.GetBoolean())
        {
            return true;
        }

        return false;
    }

    public async Task<string> SendMessageAsync(string userMessage)
    {
        var (isSafe, reason) = await EvaluateContentSafetyAsync(userMessage);
        if (!isSafe)
        {
            _logger.LogWarning("Blocked unsafe message. Reason: {Reason}", reason);
            return "I'm sorry, but I can't process that message as it may contain inappropriate content. Please rephrase your question and try again.";
        }

        var endpoint = _configuration["AiFoundry:Endpoint"]
            ?? throw new InvalidOperationException("AiFoundry:Endpoint is not configured.");

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

        var tokenResult = await _credential.GetTokenAsync(
            new TokenRequestContext(new[] { "https://cognitiveservices.azure.com/.default" }),
            CancellationToken.None);

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenResult.Token);

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
