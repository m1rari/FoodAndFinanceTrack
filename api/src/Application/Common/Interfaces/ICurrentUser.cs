namespace FinanceFoodTracker.Application.Common.Interfaces;

public interface ICurrentUser
{
    Guid UserId { get; }
    long TelegramId { get; }
}
