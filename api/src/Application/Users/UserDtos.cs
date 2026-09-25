namespace FinanceFoodTracker.Application.Users;

public sealed record UserDto(Guid Id, long TelegramId, string? Username);

public sealed record TelegramAuthRequest(string InitData);
