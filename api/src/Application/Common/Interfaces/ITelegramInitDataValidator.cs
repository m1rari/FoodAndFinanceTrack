using FinanceFoodTracker.Application.Common.Models;

namespace FinanceFoodTracker.Application.Common.Interfaces;

public interface ITelegramInitDataValidator
{
    bool TryValidate(string initData, out TelegramUser? user);
}
