using System.Net.Http.Json;
using System.Text.Json;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure.Telegram;

public sealed class TelegramBotClient : ITelegramBot
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotClient> _logger;

    public TelegramBotClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramBotClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        var body = new { chat_id = chatId, text, disable_web_page_preview = true };
        await SendAsync(body, cancellationToken);
    }

    public async Task SendKeyboardAsync(long chatId, string text, IReadOnlyList<IReadOnlyList<string>> rows, CancellationToken cancellationToken = default)
    {
        var keyboard = rows
            .Select(row => row.Select(label => new { text = label }).ToArray())
            .ToArray();

        var body = new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                keyboard,
                resize_keyboard = true,
                is_persistent = true
            }
        };

        await SendAsync(body, cancellationToken);
    }

    private async Task SendAsync(object body, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("sendMessage", body, SerializerOptions, cancellationToken);
            await EnsureOkAsync(response, "sendMessage", cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Не удалось обратиться к Telegram API.");
        }
    }

    public async Task<byte[]?> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _http.GetAsync($"getFile?file_id={Uri.EscapeDataString(fileId)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram getFile вернул {Status}", (int)response.StatusCode);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<TelegramFileResponse>(SerializerOptions, cancellationToken);
            var filePath = payload?.Result?.FilePath;

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return null;
            }

            var fileUrl = $"https://api.telegram.org/file/bot{_options.BotToken}/{filePath}";
            return await _http.GetByteArrayAsync(fileUrl, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Не удалось обратиться к Telegram API.");
        }
    }

    public async Task SetWebhookAsync(string url, string? secretToken, CancellationToken cancellationToken = default)
    {
        object body = string.IsNullOrWhiteSpace(secretToken)
            ? new { url, allowed_updates = new[] { "message" } }
            : new { url, secret_token = secretToken, allowed_updates = new[] { "message" } };

        try
        {
            var response = await _http.PostAsJsonAsync("setWebhook", body, SerializerOptions, cancellationToken);
            await EnsureOkAsync(response, "setWebhook", cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException("Не удалось обратиться к Telegram API.");
        }
    }

    private static async Task EnsureOkAsync(HttpResponseMessage response, string method, CancellationToken cancellationToken)
    {
        TelegramResponse? payload = null;

        try
        {
            payload = await response.Content.ReadFromJsonAsync<TelegramResponse>(SerializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // тело не JSON — обработаем по статусу ниже
        }

        if (!response.IsSuccessStatusCode || payload is { Ok: false })
        {
            throw new InvalidOperationException(
                $"Telegram {method} failed: {(int)response.StatusCode} {payload?.Description}");
        }
    }
}
