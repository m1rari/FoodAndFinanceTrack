using FinanceFoodTracker.Application.Categories;
using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;
    private readonly ICurrentUser _currentUser;

    public CategoriesController(ICategoryService categories, ICurrentUser currentUser)
    {
        _categories = categories;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> Get([FromQuery] string? type, CancellationToken cancellationToken)
    {
        TransactionType? parsed = null;

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse<TransactionType>(type, ignoreCase: true, out var value))
            {
                throw new ValidationException($"Неизвестный тип категории: {type}");
            }

            parsed = value;
        }

        return Ok(await _categories.GetAsync(_currentUser.UserId, parsed, cancellationToken));
    }
}
