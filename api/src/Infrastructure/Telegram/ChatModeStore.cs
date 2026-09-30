using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Infrastructure.Telegram;

public sealed class ChatModeStore : IChatModeStore
{
    private readonly IApplicationDbContext _db;

    public ChatModeStore(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ChatMode> GetAsync(long chatId, CancellationToken cancellationToken = default)
    {
        var state = await _db.ChatModes.FirstOrDefaultAsync(c => c.ChatId == chatId, cancellationToken);
        return state is null ? ChatMode.None : (ChatMode)state.Mode;
    }

    public async Task SetAsync(long chatId, ChatMode mode, CancellationToken cancellationToken = default)
    {
        var state = await _db.ChatModes.FirstOrDefaultAsync(c => c.ChatId == chatId, cancellationToken);

        if (state is null)
        {
            _db.ChatModes.Add(new ChatModeState
            {
                ChatId = chatId,
                Mode = (int)mode,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            state.Mode = (int)mode;
            state.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
