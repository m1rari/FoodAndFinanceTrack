namespace FinanceFoodTracker.Application.Categories;

public sealed record CategoryDto(Guid Id, string Name, string Type, Guid? ParentId, bool IsSystem);
