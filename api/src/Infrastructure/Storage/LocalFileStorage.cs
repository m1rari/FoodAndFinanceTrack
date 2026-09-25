using FinanceFoodTracker.Application.Common.Exceptions;
using FinanceFoodTracker.Application.Common.Interfaces;
using FinanceFoodTracker.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FinanceFoodTracker.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var relativePath = Path.Combine(now.ToString("yyyy"), now.ToString("MM"), $"{Guid.NewGuid():N}{extension}");
        var fullPath = Resolve(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        return relativePath.Replace('\\', '/');
    }

    public async Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default)
        => await File.ReadAllBytesAsync(Resolve(path), cancellationToken);

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        var fullPath = Resolve(path);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public bool Exists(string path) => File.Exists(Resolve(path));

    private string Resolve(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_root, relativePath));

        if (!fullPath.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Некорректный путь к файлу.");
        }

        return fullPath;
    }
}
