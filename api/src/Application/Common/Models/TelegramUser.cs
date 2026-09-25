namespace FinanceFoodTracker.Application.Common.Models;

public sealed record TelegramUser(long Id, string? Username, string? FirstName, string? LastName);
