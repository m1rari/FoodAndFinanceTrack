namespace FinanceFoodTracker.Application.Common.Interfaces;

public enum ChatMode
{
    None = 0,
    Receipt = 1,
    Food = 2
}

public interface IChatModeStore
{
    Task<ChatMode> GetAsync(long chatId, CancellationToken cancellationToken = default);

    Task SetAsync(long chatId, ChatMode mode, CancellationToken cancellationToken = default);
}
