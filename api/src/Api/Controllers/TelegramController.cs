using FinanceFoodTracker.Api.Telegram;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public sealed class TelegramController : ControllerBase
{
    public const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";

    private readonly IUserService _users;
    private readonly IReceiptService _receipts;
    private readonly ITelegramBot _bot;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        IUserService users,
        IReceiptService receipts,
        ITelegramBot bot,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger)
    {
        _users = users;
        _receipts = receipts;
        _bot = bot;
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] TelegramUpdate update, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            if (!Request.Headers.TryGetValue(SecretHeader, out var secret)
                || secret.ToString() != _options.WebhookSecret)
            {
                return Unauthorized();
            }
        }

        var message = update.Message;

        if (message?.Chat is null || message.From is null)
        {
            return Ok();
        }

        var chatId = message.Chat.Id;
        var fileId = ResolveFileId(message);

        if (fileId is null)
        {
            await SafeSendAsync(chatId, "Пришлите фото чека — я распознаю товары и цены.", cancellationToken);
            return Ok();
        }

        try
        {
            var bytes = await _bot.DownloadFileAsync(fileId, cancellationToken);

            if (bytes is null || bytes.Length == 0)
            {
                await SafeSendAsync(chatId, "Не удалось скачать файл. Попробуйте ещё раз.", cancellationToken);
                return Ok();
            }

            var user = await _users.GetOrCreateAsync(message.From.Id, message.From.Username, cancellationToken);
            var fileName = message.Document?.FileName ?? "receipt.jpg";

            await _receipts.CreateAsync(user.Id, bytes, fileName, cancellationToken, chatId);
            await SafeSendAsync(chatId, "Чек принят, распознаю…", cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Ошибка обработки webhook телеграм-бота");
            await SafeSendAsync(chatId, "Ошибка обработки. Попробуйте позже.", cancellationToken);
        }

        return Ok();
    }

    private static string? ResolveFileId(TelegramMessage message)
    {
        if (message.Photo is { Count: > 0 })
        {
            return message.Photo[^1].FileId;
        }

        if (message.Document is { } document
            && document.MimeType is not null
            && document.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return document.FileId;
        }

        return null;
    }

    private async Task SafeSendAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await _bot.SendMessageAsync(chatId, text, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось отправить сообщение в Telegram чат {ChatId}", chatId);
        }
    }
}
