using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Models;
using FinanceFoodTracker.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure.Security;

public sealed class TelegramInitDataValidator : ITelegramInitDataValidator
{
    private readonly TelegramOptions _options;

    public TelegramInitDataValidator(IOptions<TelegramOptions> options)
    {
        _options = options.Value;
    }

    public bool TryValidate(string initData, out TelegramUser? user)
    {
        user = null;

        if (string.IsNullOrWhiteSpace(initData) || string.IsNullOrWhiteSpace(_options.BotToken))
        {
            return false;
        }

        var pairs = ParseQuery(initData);

        if (!pairs.TryGetValue("hash", out var hash) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        pairs.Remove("hash");

        var dataCheckString = string.Join("\n", pairs
            .OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"{p.Key}={p.Value}"));

        using var hmacSecret = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = hmacSecret.ComputeHash(Encoding.UTF8.GetBytes(_options.BotToken));

        using var hmacSignature = new HMACSHA256(secretKey);
        var computedHash = Convert.ToHexString(hmacSignature.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();

        if (!FixedTimeEquals(computedHash, hash))
        {
            return false;
        }

        if (pairs.TryGetValue("auth_date", out var authDateRaw) &&
            long.TryParse(authDateRaw, out var authDateUnix))
        {
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
            if (DateTimeOffset.UtcNow - issuedAt > _options.InitDataTtl)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        if (!pairs.TryGetValue("user", out var userJson) || string.IsNullOrWhiteSpace(userJson))
        {
            return false;
        }

        var parsed = JsonSerializer.Deserialize<TelegramUserJson>(userJson);
        if (parsed is null || parsed.Id == 0)
        {
            return false;
        }

        user = new TelegramUser(parsed.Id, parsed.Username, parsed.FirstName, parsed.LastName);
        return true;
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
               CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var index = pair.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var key = WebUtility.UrlDecode(pair[..index]);
            var value = WebUtility.UrlDecode(pair[(index + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private sealed class TelegramUserJson
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }
    }
}
