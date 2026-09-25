using FinanceFoodTracker.Domain.Enums;

namespace FinanceFoodTracker.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAsync(Guid userId, TransactionType? type, CancellationToken cancellationToken = default);
}
