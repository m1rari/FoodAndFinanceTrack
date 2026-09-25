using System.Security.Cryptography;
using System.Text;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceFoodTracker.Tests.Security;

public sealed class TelegramInitDataValidatorTests
{
    private const string Token = "12345:TEST-BOT-TOKEN";

    private static TelegramInitDataValidator CreateValidator(TimeSpan? ttl = null) =>
        new(Options.Create(new TelegramOptions
        {
            BotToken = Token,
            InitDataTtl = ttl ?? TimeSpan.FromHours(24)
        }));

    private static string BuildInitData(string token, long userId, string username, DateTimeOffset authDate)
    {
        var pairs = new Dictionary<string, string>
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(),
            ["query_id"] = "AAHdF6IQAAAAAN0XohDhrOrc",
            ["user"] = $"{{\"id\":{userId},\"username\":\"{username}\",\"first_name\":\"Test\"}}"
        };

        var dataCheckString = string.Join("\n", pairs
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}"));

        using var hmacSecret = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = hmacSecret.ComputeHash(Encoding.UTF8.GetBytes(token));

        using var hmacSignature = new HMACSHA256(secretKey);
        var hash = Convert.ToHexString(hmacSignature.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        var query = string.Join("&", pairs.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
        return $"{query}&hash={hash}";
    }

    [Fact]
    public void TryValidate_ValidData_ReturnsUser()
    {
        var validator = CreateValidator();
        var initData = BuildInitData(Token, 777, "john", DateTimeOffset.UtcNow);

        var result = validator.TryValidate(initData, out var user);

        Assert.True(result);
        Assert.NotNull(user);
        Assert.Equal(777, user!.Id);
        Assert.Equal("john", user.Username);
    }

    [Fact]
    public void TryValidate_WrongToken_ReturnsFalse()
    {
        var validator = CreateValidator();
        var initData = BuildInitData("other-token", 777, "john", DateTimeOffset.UtcNow);

        Assert.False(validator.TryValidate(initData, out _));
    }

    [Fact]
    public void TryValidate_ExpiredData_ReturnsFalse()
    {
        var validator = CreateValidator(TimeSpan.FromMinutes(5));
        var initData = BuildInitData(Token, 777, "john", DateTimeOffset.UtcNow.AddHours(-2));

        Assert.False(validator.TryValidate(initData, out _));
    }

    [Fact]
    public void TryValidate_MissingHash_ReturnsFalse()
    {
        var validator = CreateValidator();

        Assert.False(validator.TryValidate("auth_date=1&user=%7B%7D", out _));
    }
}
