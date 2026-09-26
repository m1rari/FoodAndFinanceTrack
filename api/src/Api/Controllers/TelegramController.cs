using FinanceFoodTracker.Api.Telegram;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodLogs;
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

    private static readonly IReadOnlyList<IReadOnlyList<string>> Menu = new[]
    {
        new[] { "Чек", "Еда" }
    };

    private readonly IUserService _users;
    private readonly IReceiptService _receipts;
    private readonly IFoodLogService _foodLogs;
    private readonly ITelegramBot _bot;
    private readonly IChatModeStore _modes;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramController> _logger;

    public TelegramController(
        IUserService users,
        IReceiptService receipts,
        IFoodLogService foodLogs,
        ITelegramBot bot,
        IChatModeStore modes,
        IOptions<TelegramOptions> options,
        ILogger<TelegramController> logger)
    {
        _users = users;
        _receipts = receipts;
        _foodLogs = foodLogs;
        _bot = bot;
        _modes = modes;
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
        var text = message.Text?.Trim();
        var mode = _modes.Get(chatId);

        if (await HandleCommandsAsync(chatId, text, cancellationToken))
        {
            return Ok();
        }

        if (message.Voice is not null || message.Audio is not null)
        {
            await SafeSendAsync(chatId, "Голосовые сообщения пока не поддерживаются — пришлите фото или напишите текстом.", cancellationToken);
            return Ok();
        }

        var fileId = ResolveFileId(message);

        if (fileId is not null)
        {
            await HandlePhotoAsync(chatId, message.From.Id, message.From.Username, fileId, message.Document?.FileName, message.Caption, mode, cancellationToken);
            return Ok();
        }

        if (!string.IsNullOrEmpty(text))
        {
            await HandleTextAsync(chatId, message.From.Id, message.From.Username, text, mode, cancellationToken);
            return Ok();
        }

        await SendMenuAsync(chatId, "Не понял сообщение. Пришлите фото или выберите: чек или еда.", cancellationToken);
        return Ok();
    }

    private async Task<bool> HandleCommandsAsync(long chatId, string? text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.StartsWith('/'))
        {
            switch (text)
            {
                case "/start":
                case "/menu":
                case "/help":
                    _modes.Set(chatId, ChatMode.None);
                    await SendMenuAsync(chatId, "Привет! Что обработать — чек или еда? Выберите кнопкой или просто напишите.", cancellationToken);
                    return true;
                case "/receipt":
                    _modes.Set(chatId, ChatMode.Receipt);
                    await SendMenuAsync(chatId, "Режим: чек. Жду фото чека или его описание текстом.", cancellationToken);
                    return true;
                case "/food":
                    _modes.Set(chatId, ChatMode.Food);
                    await SendMenuAsync(chatId, "Режим: еда. Жду фото блюда или его описание текстом.", cancellationToken);
                    return true;
                default:
                    await SendMenuAsync(chatId, "Доступные команды: /start, /receipt, /food.", cancellationToken);
                    return true;
            }
        }

        if (string.Equals(text, "чек", StringComparison.OrdinalIgnoreCase))
        {
            _modes.Set(chatId, ChatMode.Receipt);
            await SendMenuAsync(chatId, "Режим: чек. Жду фото чека или его описание текстом.", cancellationToken);
            return true;
        }

        if (string.Equals(text, "еда", StringComparison.OrdinalIgnoreCase))
        {
            _modes.Set(chatId, ChatMode.Food);
            await SendMenuAsync(chatId, "Режим: еда. Жду фото блюда или его описание текстом.", cancellationToken);
            return true;
        }

        return false;
    }

    private async Task HandlePhotoAsync(
        long chatId,
        long userId,
        string? username,
        string fileId,
        string? fileName,
        string? caption,
        ChatMode mode,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _bot.DownloadFileAsync(fileId, cancellationToken);

            if (bytes is null || bytes.Length == 0)
            {
                await SafeSendAsync(chatId, "Не удалось скачать файл. Попробуйте ещё раз.", cancellationToken);
                return;
            }

            var user = await _users.GetOrCreateAsync(userId, username, cancellationToken);
            var name = fileName ?? "photo.jpg";

            if (mode == ChatMode.Food)
            {
                await _foodLogs.CreateAsync(user.Id, bytes, name, caption, cancellationToken, chatId);
                await SafeSendAsync(chatId, "Блюдо принято, оцениваю…", cancellationToken);
            }
            else
            {
                await _receipts.CreateAsync(user.Id, bytes, name, cancellationToken, chatId);
                await SafeSendAsync(chatId, "Чек принят, распознаю…", cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Ошибка обработки фото из Telegram (чат {ChatId})", chatId);
            await SafeSendAsync(chatId, "Ошибка обработки. Попробуйте позже.", cancellationToken);
        }
    }

    private async Task HandleTextAsync(
        long chatId,
        long userId,
        string? username,
        string text,
        ChatMode mode,
        CancellationToken cancellationToken)
    {
        if (mode == ChatMode.None)
        {
            await SendMenuAsync(chatId, "Сначала выберите, что это — чек или еда.", cancellationToken);
            return;
        }

        try
        {
            var user = await _users.GetOrCreateAsync(userId, username, cancellationToken);

            if (mode == ChatMode.Food)
            {
                await _foodLogs.CreateFromTextAsync(user.Id, text, cancellationToken, chatId);
                await SafeSendAsync(chatId, "Описание принято, оцениваю…", cancellationToken);
            }
            else
            {
                await _receipts.CreateFromTextAsync(user.Id, text, cancellationToken, chatId);
                await SafeSendAsync(chatId, "Описание принято, распознаю…", cancellationToken);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "Ошибка обработки текста из Telegram (чат {ChatId})", chatId);
            await SafeSendAsync(chatId, "Ошибка обработки. Попробуйте позже.", cancellationToken);
        }
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

    private async Task SendMenuAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        try
        {
            await _bot.SendKeyboardAsync(chatId, text, Menu, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Не удалось отправить меню в Telegram чат {ChatId}", chatId);
        }
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
