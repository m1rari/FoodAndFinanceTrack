using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure.Analysis;

public sealed class OpenCodeGoVisionClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly OpenCodeGoOptions _options;

    public OpenCodeGoVisionClient(HttpClient http, IOptions<OpenCodeGoOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userText,
        string imageDataUrl,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Ключ OpenCode Go API не настроен.");
        }

        var completion = await CompleteAsync(systemPrompt, userText, imageDataUrl, sessionId, useResponseFormat: true, cancellationToken);
        var content = completion?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("AI-провайдер вернул пустой ответ.");
        }

        return content;
    }

    private async Task<ChatCompletionResponse?> CompleteAsync(
        string systemPrompt,
        string userText,
        string imageDataUrl,
        string sessionId,
        bool useResponseFormat,
        CancellationToken cancellationToken)
    {
        var body = new ChatCompletionRequest
        {
            Model = _options.Model,
            RequestFormat = useResponseFormat ? new ResponseFormat() : null,
            Messages = new List<ChatMessage>
            {
                new()
                {
                    Role = "system",
                    Content = new List<ContentPart> { new() { Type = "text", Text = systemPrompt } }
                },
                new()
                {
                    Role = "user",
                    Content = new List<ContentPart>
                    {
                        new() { Type = "text", Text = userText },
                        new() { Type = "image_url", ImageUrl = new ImageUrl { Url = imageDataUrl } }
                    }
                }
            }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = JsonContent.Create(body, options: SerializerOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Headers.TryAddWithoutValidation("x-opencode-session", sessionId);

        using var response = await _http.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (useResponseFormat && response.StatusCode == HttpStatusCode.BadRequest)
            {
                return await CompleteAsync(systemPrompt, userText, imageDataUrl, sessionId, useResponseFormat: false, cancellationToken);
            }

            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"AI-провайдер вернул {(int)response.StatusCode}: {Truncate(error, 300)}");
        }

        return await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(SerializerOptions, cancellationToken);
    }

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..length];
}
