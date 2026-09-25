using FinanceFoodTracker.Application.Users;
using Xunit;

namespace FinanceFoodTracker.Tests.Users;

public sealed class UserServiceTests
{
    [Fact]
    public async Task GetOrCreate_CreatesUserAndAccount()
    {
        await using var db = TestDb.Create();
        var service = new UserService(db);

        var user = await service.GetOrCreateAsync(123, "alice");

        Assert.Equal(123, user.TelegramId);
        Assert.Equal("alice", user.Username);
        Assert.Single(db.Accounts);
    }

    [Fact]
    public async Task GetOrCreate_ExistingUser_ReturnsSame()
    {
        await using var db = TestDb.Create();
        var service = new UserService(db);

        var first = await service.GetOrCreateAsync(123, "alice");
        var second = await service.GetOrCreateAsync(123, "alice_updated");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("alice_updated", second.Username);
        Assert.Single(db.Users);
        Assert.Single(db.Accounts);
    }
}
