using System.Collections.Concurrent;
using FinanceFoodTracker.Application.Common.Interfaces;

namespace FinanceFoodTracker.Infrastructure.Telegram;

public sealed class InMemoryChatModeStore : IChatModeStore
{
    private readonly ConcurrentDictionary<long, ChatMode> _modes = new();

    public ChatMode Get(long chatId) => _modes.TryGetValue(chatId, out var mode) ? mode : ChatMode.None;

    public void Set(long chatId, ChatMode mode) => _modes[chatId] = mode;
}
