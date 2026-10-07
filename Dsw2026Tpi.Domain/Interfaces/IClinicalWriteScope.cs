namespace Dsw2026Tpi.Domain.Interfaces;

/// <summary>
/// Representa una operación de escritura clínica transaccional.
/// Si se libera sin completar, sus cambios se revierten.
/// </summary>
public interface IClinicalWriteScope : IAsyncDisposable
{
    /// <summary>
    /// Guarda los cambios pendientes y confirma la transacción.
    /// </summary>
    Task CompleteAsync(
        CancellationToken cancellationToken = default);
}  
