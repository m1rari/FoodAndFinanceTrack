namespace FinanceFoodTracker.Application.Common.Interfaces;

public interface IFileStorage
{
    Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default);

    Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default);

    bool Exists(string path);
}
