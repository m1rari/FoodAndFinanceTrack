using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Application.SavedDishes;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/saved-dishes")]
public sealed class SavedDishesController : ControllerBase
{
    private readonly ISavedDishService _savedDishes;
    private readonly ICurrentUser _currentUser;

    public SavedDishesController(ISavedDishService savedDishes, ICurrentUser currentUser)
    {
        _savedDishes = savedDishes;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedDishDto>>> Get(
        [FromQuery] bool? favorite,
        [FromQuery] int limit,
        CancellationToken cancellationToken)
        => Ok(await _savedDishes.GetAsync(_currentUser.UserId, favorite, limit, cancellationToken));

    [HttpPost("{id:guid}/diary")]
    public async Task<ActionResult<FoodLogDto>> AddToDiary(Guid id, CancellationToken cancellationToken)
        => Ok(await _savedDishes.AddToDiaryAsync(_currentUser.UserId, id, cancellationToken));

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<SavedDishDto>> SetFavorite(Guid id, [FromBody] SetFavoriteRequest request, CancellationToken cancellationToken)
        => Ok(await _savedDishes.SetFavoriteAsync(_currentUser.UserId, id, request.IsFavorite, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _savedDishes.DeleteAsync(_currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
