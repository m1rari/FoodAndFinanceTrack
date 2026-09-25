using FinanceFoodTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceFoodTracker.Tests;

internal static class TestDb
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
