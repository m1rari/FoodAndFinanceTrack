using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Users;

public sealed class UserService : IUserService
{
    private readonly IApplicationDbContext _db;

    public UserService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<User> GetOrCreateAsync(long telegramId, string? username, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.TelegramId == telegramId, cancellationToken);
        if (user is not null)
        {
            if (!string.IsNullOrWhiteSpace(username) && user.Username != username)
            {
                user.Username = username;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return user;
        }

        user = new User
        {
            TelegramId = telegramId,
            Username = username
        };

        _db.Users.Add(user);
        _db.Accounts.Add(new Account
        {
            UserId = user.Id,
            Name = "Основной",
            Currency = "BYN"
        });
        await _db.SaveChangesAsync(cancellationToken);

        return user;
    }
}
