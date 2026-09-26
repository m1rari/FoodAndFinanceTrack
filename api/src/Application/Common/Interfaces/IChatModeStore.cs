namespace FinanceFoodTracker.Application.Common.Interfaces;

public enum ChatMode
{
    None = 0,
    Receipt = 1,
    Food = 2
}

public interface IChatModeStore
{
    ChatMode Get(long chatId);

    void Set(long chatId, ChatMode mode);
}
