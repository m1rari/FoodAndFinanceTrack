using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Options;
using FinanceFoodTracker.Application.FoodShares;
using FinanceFoodTracker.Application.SavedDishes;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.Extensions.Options;
using Xunit;

namespace FinanceFoodTracker.Tests.FoodShares;

public sealed class FoodShareServiceTests
{
    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Guid OwnerId, Guid RecipientId, FoodLog Log)> SeedAsync()
    {
        var db = TestDb.Create();
        var owner = new User { TelegramId = 1, Username = "owner" };
        var recipient = new User { TelegramId = 2, Username = "recipient" };
        var log = new FoodLog
        {
            UserId = owner.Id,
            ImagePath = string.Empty,
            DishName = "Борщ",
            CaloriesMin = 300,
            CaloriesMax = 400,
            ProteinG = 10,
            Status = ProcessingStatus.Processed
        };

        db.Users.AddRange(owner, recipient);
        db.FoodLogs.Add(log);
        await db.SaveChangesAsync();

        return (db, owner.Id, recipient.Id, log);
    }

    private static FoodShareService CreateService(Infrastructure.Persistence.AppDbContext db)
        => new(db, new SavedDishService(db), Options.Create(new TelegramOptions { BotUsername = "testbot" }));

    [Fact]
    public async Task Create_And_Claim_CopiesDishToRecipient()
    {
        var (db, ownerId, recipientId, log) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db);

        var share = await service.CreateAsync(ownerId, log.Id);

        Assert.False(string.IsNullOrWhiteSpace(share.Token));
        Assert.NotNull(share.Url);
        Assert.Contains(share.Token, share.Url!);

        var claimed = await service.ClaimAsync(recipientId, share.Token);

        Assert.Equal("Борщ", claimed.DishName);
        Assert.Equal(400m, claimed.CaloriesMax);
        Assert.True(db.SavedDishes.Any(d => d.UserId == recipientId && d.Name == "Борщ"));
    }

    [Fact]
    public async Task Claim_Expired_Throws()
    {
        var (db, ownerId, recipientId, log) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db);
        var share = await service.CreateAsync(ownerId, log.Id);

        var entity = db.FoodShares.First(s => s.Token == share.Token);
        entity.ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() => service.ClaimAsync(recipientId, share.Token));
    }

    [Fact]
    public async Task Claim_UnknownToken_Throws()
    {
        var (db, _, recipientId, _) = await SeedAsync();
        await using var _ = db;
        var service = CreateService(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ClaimAsync(recipientId, "nope"));
    }
}
