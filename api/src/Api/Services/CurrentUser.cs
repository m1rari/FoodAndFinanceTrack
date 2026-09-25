using FinanceFoodTracker.Application.Common.Interfaces;

namespace FinanceFoodTracker.Api.Services;

public sealed class CurrentUser : ICurrentUser
{
    public Guid UserId { get; private set; }
    public long TelegramId { get; private set; }
    public bool IsAuthenticated { get; private set; }

    public void Set(Guid userId, long telegramId)
    {
        UserId = userId;
        TelegramId = telegramId;
        IsAuthenticated = true;
    }
}
