namespace FinanceFoodTracker.Application.Common.Interfaces;

public interface ITelegramBot
{
    Task SendMessageAsync(long chatId, string text, CancellationToken cancellationToken = default);

    Task<byte[]?> DownloadFileAsync(string fileId, CancellationToken cancellationToken = default);

    Task SetWebhookAsync(string url, string? secretToken, CancellationToken cancellationToken = default);
}
