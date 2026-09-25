using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ITelegramInitDataValidator _validator;
    private readonly IUserService _userService;

    public AuthController(ITelegramInitDataValidator validator, IUserService userService)
    {
        _validator = validator;
        _userService = userService;
    }

    [HttpPost("telegram")]
    [AllowAnonymous]
    public async Task<ActionResult<UserDto>> Telegram([FromBody] TelegramAuthRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InitData) ||
            !_validator.TryValidate(request.InitData, out var telegramUser) ||
            telegramUser is null)
        {
            return Unauthorized(new { error = "initData недействителен." });
        }

        var user = await _userService.GetOrCreateAsync(telegramUser.Id, telegramUser.Username, cancellationToken);

        return Ok(new UserDto(user.Id, user.TelegramId, user.Username));
    }
}
