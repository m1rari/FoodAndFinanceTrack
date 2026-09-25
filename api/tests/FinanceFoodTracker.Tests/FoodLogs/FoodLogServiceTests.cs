using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.FoodLogs;
using FinanceFoodTracker.Domain.Entities;
using FinanceFoodTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FinanceFoodTracker.Tests.FoodLogs;

public sealed class FoodLogServiceTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

    private sealed class FakeFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new();

        public List<string> Deleted { get; } = new();

        public Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default)
        {
            var path = $"{Guid.NewGuid():N}{extension}";
            _files[path] = content;
            return Task.FromResult(path);
        }

        public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
            => Task.FromResult(_files[path]);

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Deleted.Add(path);
            _files.Remove(path);
            return Task.CompletedTask;
        }

        public bool Exists(string path) => _files.ContainsKey(path);
    }

    private sealed class FakeQueue : IFoodLogProcessingQueue
    {
        public List<Guid> Enqueued { get; } = new();

        public ValueTask EnqueueAsync(Guid foodLogId, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(foodLogId);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Guid> ReadAllAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var id in Enqueued)
            {
                yield return id;
            }

            await Task.CompletedTask;
        }
    }

    private static async Task<(Infrastructure.Persistence.AppDbContext Db, Guid UserId)> SeedAsync()
    {
        var db = TestDb.Create();
        var user = new User { TelegramId = 999, Username = "foodie" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (db, user.Id);
    }

    [Fact]
    public async Task Create_ValidJpeg_SavesAndEnqueues()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var storage = new FakeFileStorage();
        var queue = new FakeQueue();
        var service = new FoodLogService(db, storage, queue);

        var result = await service.CreateAsync(userId, JpegBytes, "dish.jpg");

        Assert.Equal("Pending", result.Status);
        Assert.Contains(result.Id, queue.Enqueued);
        var log = await db.FoodLogs.FindAsync(result.Id);
        Assert.NotNull(log);
        Assert.True(storage.Exists(log!.ImagePath));
    }

    [Fact]
    public async Task Create_WithContext_StoresIt()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var service = new FoodLogService(db, new FakeFileStorage(), new FakeQueue());

        var result = await service.CreateAsync(userId, JpegBytes, "dish.jpg", context: "  омлет с сыром  ");

        Assert.Equal("омлет с сыром", result.UserContext);
    }

    [Fact]
    public async Task Reanalyze_UpdatesContextAndEnqueues()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var queue = new FakeQueue();
        var service = new FoodLogService(db, new FakeFileStorage(), queue);

        var created = await service.CreateAsync(userId, JpegBytes, "dish.jpg", context: "суп");
        queue.Enqueued.Clear();

        var result = await service.ReanalyzeAsync(userId, created.Id, "борщ со сметаной");

        Assert.Equal("Pending", result.Status);
        Assert.Equal("борщ со сметаной", result.UserContext);
        Assert.Contains(created.Id, queue.Enqueued);
    }

    [Fact]
    public async Task Create_UnsupportedContent_Throws()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var service = new FoodLogService(db, new FakeFileStorage(), new FakeQueue());

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(userId, new byte[] { 0x25, 0x50, 0x44, 0x46 }, "file.pdf"));
    }

    [Fact]
    public async Task Update_RecomputesMidpoints()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var service = new FoodLogService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "dish.jpg");

        var updated = await service.UpdateAsync(userId, created.Id, new UpdateFoodLogRequest(
            DishName: "Паста",
            CaloriesMin: 400,
            CaloriesMax: 600,
            ProteinMinG: 10,
            ProteinMaxG: 20));

        Assert.Equal("Паста", updated.DishName);
        Assert.Equal(500m, (updated.CaloriesMin!.Value + updated.CaloriesMax!.Value) / 2m);
        Assert.Equal(15m, updated.ProteinG);
    }

    [Fact]
    public async Task Update_NegativeValue_Throws()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var service = new FoodLogService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "dish.jpg");

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateAsync(userId, created.Id, new UpdateFoodLogRequest(CaloriesMin: -1)));
    }

    [Fact]
    public async Task Get_FiltersByPeriod()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var service = new FoodLogService(db, new FakeFileStorage(), new FakeQueue());

        await service.CreateAsync(userId, JpegBytes, "dish.jpg");

        var now = DateTimeOffset.UtcNow;
        var inRange = await service.GetAsync(userId, now.AddHours(-1), now.AddHours(1));
        var outOfRange = await service.GetAsync(userId, now.AddDays(1), now.AddDays(2));

        Assert.Single(inRange);
        Assert.Empty(outOfRange);
    }

    [Fact]
    public async Task Delete_RemovesLogAndFile()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var storage = new FakeFileStorage();
        var service = new FoodLogService(db, storage, new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "dish.jpg");
        var path = (await db.FoodLogs.FindAsync(created.Id))!.ImagePath;

        await service.DeleteAsync(userId, created.Id);

        Assert.Null(await db.FoodLogs.FindAsync(created.Id));
        Assert.Contains(path, storage.Deleted);
    }

    [Fact]
    public async Task Get_OtherUser_Throws()
    {
        var (db, userId) = await SeedAsync();
        await using var _1 = db;
        var service = new FoodLogService(db, new FakeFileStorage(), new FakeQueue());

        var created = await service.CreateAsync(userId, JpegBytes, "dish.jpg");

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(Guid.NewGuid(), created.Id));
    }
}
