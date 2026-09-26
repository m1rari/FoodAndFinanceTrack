using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Statements;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/statements")]
public sealed class StatementsController : ControllerBase
{
    private const long MaxUploadBytes = 12 * 1024 * 1024;

    private readonly IStatementService _statements;
    private readonly ICurrentUser _currentUser;

    public StatementsController(IStatementService statements, ICurrentUser currentUser)
    {
        _statements = statements;
        _currentUser = currentUser;
    }

    [HttpPost]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes + 1024 * 1024)]
    public async Task<ActionResult<StatementDto>> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException("Файл не передан.");
        }

        if (file.Length > MaxUploadBytes)
        {
            throw new ValidationException("Размер файла не должен превышать 12 МБ.");
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var created = await _statements.CreateAsync(_currentUser.UserId, buffer.ToArray(), file.FileName, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StatementDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await _statements.GetAsync(_currentUser.UserId, id, cancellationToken));

    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<StatementDto>> Confirm(Guid id, [FromBody] ConfirmStatementRequest request, CancellationToken cancellationToken)
        => Ok(await _statements.ConfirmAsync(_currentUser.UserId, id, request, cancellationToken));
}
