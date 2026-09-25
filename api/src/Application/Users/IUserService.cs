using FinanceFoodTracker.Domain.Entities;

namespace FinanceFoodTracker.Application.Users;

public interface IUserService
{
    Task<User> GetOrCreateAsync(long telegramId, string? username, CancellationToken cancellationToken = default);
}
