using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Transactions;
using Microsoft.AspNetCore.Mvc;

namespace FinanceFoodTracker.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactions;
    private readonly ICurrentUser _currentUser;

    public TransactionsController(ITransactionService transactions, ICurrentUser currentUser)
    {
        _transactions = transactions;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> Get(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        var filter = new TransactionFilter(from, to, categoryId, type);
        return Ok(await _transactions.GetAsync(_currentUser.UserId, filter, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<TransactionDto>> Create([FromBody] CreateTransactionRequest request, CancellationToken cancellationToken)
    {
        var created = await _transactions.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TransactionDto>> Update(Guid id, [FromBody] UpdateTransactionRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _transactions.UpdateAsync(_currentUser.UserId, id, request, cancellationToken));
    }
}
