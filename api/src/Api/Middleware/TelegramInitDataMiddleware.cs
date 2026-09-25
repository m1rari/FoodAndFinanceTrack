using FinanceFoodTracker.Api.Services;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Users;

namespace FinanceFoodTracker.Api.Middleware;

public sealed class TelegramInitDataMiddleware
{
    public const string HeaderName = "X-Telegram-Init-Data";

    private readonly RequestDelegate _next;

    public TelegramInitDataMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITelegramInitDataValidator validator,
        IUserService userService,
        CurrentUser currentUser)
    {
        var path = context.Request.Path;

        if (!path.StartsWithSegments("/api") || path.StartsWithSegments("/api/auth"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var initData) || string.IsNullOrWhiteSpace(initData))
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "Заголовок с initData не передан.");
            return;
        }

        if (!validator.TryValidate(initData.ToString(), out var telegramUser) || telegramUser is null)
        {
            await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "initData недействителен.");
            return;
        }

        var user = await userService.GetOrCreateAsync(telegramUser.Id, telegramUser.Username, context.RequestAborted);
        currentUser.Set(user.Id, user.TelegramId);

        await _next(context);
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
