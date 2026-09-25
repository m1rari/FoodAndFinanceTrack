using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/food-logs")]
public sealed class FoodLogsController : ControllerBase
{
    private const long MaxUploadBytes = 8 * 1024 * 1024;

    private readonly IFoodLogService _foodLogs;
    private readonly ICurrentUser _currentUser;

    public FoodLogsController(IFoodLogService foodLogs, ICurrentUser currentUser)
    {
        _foodLogs = foodLogs;
        _currentUser = currentUser;
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 1024 * 1024)]
    public async Task<ActionResult<FoodLogDto>> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? context,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException("Файл не передан.");
        }

        if (file.Length > MaxUploadBytes)
        {
            throw new ValidationException("Размер файла не должен превышать 8 МБ.");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var created = await _foodLogs.CreateAsync(_currentUser.UserId, buffer.ToArray(), file.FileName, context, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FoodLogDto>>> GetList(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var fromValue = from ?? DateTimeOffset.UtcNow.Date;
        var toValue = to ?? fromValue.AddDays(1);
        return Ok(await _foodLogs.GetAsync(_currentUser.UserId, fromValue, toValue, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<FoodLogDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _foodLogs.GetByIdAsync(_currentUser.UserId, id, cancellationToken));

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var image = await _foodLogs.GetImageAsync(_currentUser.UserId, id, cancellationToken);
        return File(image.Content, image.ContentType);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<FoodLogDto>> Update(Guid id, [FromBody] UpdateFoodLogRequest request, CancellationToken cancellationToken)
        => Ok(await _foodLogs.UpdateAsync(_currentUser.UserId, id, request, cancellationToken));

    [HttpPost("{id:guid}/reanalyze")]
    public async Task<ActionResult<FoodLogDto>> Reanalyze(Guid id, [FromBody] ReanalyzeFoodLogRequest request, CancellationToken cancellationToken)
        => Ok(await _foodLogs.ReanalyzeAsync(_currentUser.UserId, id, request.Context, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _foodLogs.DeleteAsync(_currentUser.UserId, id, cancellationToken);
        return NoContent();
    }
}
