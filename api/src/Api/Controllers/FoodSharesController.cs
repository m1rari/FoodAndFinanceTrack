using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.FoodShares;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/food-shares")]
public sealed class FoodSharesController : ControllerBase
{
    private readonly IFoodShareService _shares;
    private readonly ICurrentUser _currentUser;

    public FoodSharesController(IFoodShareService shares, ICurrentUser currentUser)
    {
        _shares = shares;
        _currentUser = currentUser;
    }

    [HttpGet("{token}")]
    public async Task<ActionResult<FoodSharePreviewDto>> Get(string token, CancellationToken cancellationToken)
        => Ok(await _shares.GetAsync(token, cancellationToken));

    [HttpPost("{token}/claim")]
    public async Task<ActionResult<FoodLogDto>> Claim(string token, CancellationToken cancellationToken)
        => Ok(await _shares.ClaimAsync(_currentUser.UserId, token, cancellationToken));
}
