using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Application.Categories;

public sealed class CategoryService : ICategoryService
{
    private readonly IApplicationDbContext _db;

    public CategoryService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAsync(Guid userId, TransactionType? type, CancellationToken cancellationToken = default)
    {
        var query = _db.Categories.Where(c => c.UserId == null || c.UserId == userId);

        if (type is not null)
        {
            query = query.Where(c => c.Type == type);
        }

        return await query
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Type.ToString(), c.ParentId, c.IsSystem))
            .ToListAsync(cancellationToken);
    }
}
