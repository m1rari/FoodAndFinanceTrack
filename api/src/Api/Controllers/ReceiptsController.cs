using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Receipts;
using FinanceFoodTracker.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/receipts")]
public sealed class ReceiptsController : ControllerBase
{
    private const long MaxUploadBytes = 8 * 1024 * 1024;

    private readonly IReceiptService _receipts;
    private readonly ICurrentUser _currentUser;

    public ReceiptsController(IReceiptService receipts, ICurrentUser currentUser)
    {
        _receipts = receipts;
        _currentUser = currentUser;
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 1024 * 1024)]
    public async Task<ActionResult<ReceiptDto>> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
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

        var created = await _receipts.CreateAsync(_currentUser.UserId, buffer.ToArray(), file.FileName, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReceiptSummaryDto>>> GetList(
        [FromQuery] bool unconfirmed,
        CancellationToken cancellationToken)
        => Ok(await _receipts.GetListAsync(_currentUser.UserId, unconfirmed, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReceiptDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _receipts.GetAsync(_currentUser.UserId, id, cancellationToken));

    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var image = await _receipts.GetImageAsync(_currentUser.UserId, id, cancellationToken);
        return File(image.Content, image.ContentType);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _receipts.DeleteAsync(_currentUser.UserId, id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<ReceiptDto>> AddItem(Guid id, [FromBody] CreateReceiptItemRequest request, CancellationToken cancellationToken)
        => Ok(await _receipts.AddItemAsync(_currentUser.UserId, id, request, cancellationToken));

    [HttpPatch("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ReceiptDto>> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateReceiptItemRequest request, CancellationToken cancellationToken)
        => Ok(await _receipts.UpdateItemAsync(_currentUser.UserId, id, itemId, request, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ReceiptDto>> Confirm(Guid id, CancellationToken cancellationToken)
        => Ok(await _receipts.ConfirmAsync(_currentUser.UserId, id, cancellationToken));

    [HttpGet("{id:guid}/matches")]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetMatches(Guid id, CancellationToken cancellationToken)
        => Ok(await _receipts.GetMatchesAsync(_currentUser.UserId, id, cancellationToken));

    [HttpPost("{id:guid}/link/{transactionId:guid}")]
    public async Task<ActionResult<ReceiptDto>> Link(Guid id, Guid transactionId, CancellationToken cancellationToken)
        => Ok(await _receipts.LinkAsync(_currentUser.UserId, id, transactionId, cancellationToken));
}
