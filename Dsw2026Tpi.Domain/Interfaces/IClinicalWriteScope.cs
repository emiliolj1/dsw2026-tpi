namespace Dsw2026Tpi.Domain.Interfaces;

public interface IClinicalWriteScope : IAsyncDisposable
{
    Task CompleteAsync(
        CancellationToken cancellationToken = default);
}
