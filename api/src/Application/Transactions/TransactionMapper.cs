using FinanceFoodTracker.Domain.Entities;

namespace FinanceFoodTracker.Application.Transactions;

public static class TransactionMapper
{
    public static TransactionDto ToDto(Transaction t) => new(
        t.Id,
        t.AccountId,
        t.CategoryId,
        t.Category != null ? t.Category.Name : null,
        t.Type.ToString(),
        t.Amount,
        t.Currency,
        t.OccurredAt,
        t.Source.ToString(),
        t.Comment,
        t.ReceiptId,
        t.Receipt != null ? t.Receipt.MerchantName : null,
        t.CreatedAt);
}
