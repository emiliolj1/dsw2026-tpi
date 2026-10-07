namespace Dsw2026Tpi.Domain.Interfaces;

/// <summary>
/// Inicia un alcance de escritura sobre el mismo contexto
/// utilizado por los repositorios de la solicitud.
/// </summary>
public interface IClinicalWriteScopeFactory
{
    /// <summary>
    /// Abre la transacción y adquiere el bloqueo compartido.
    /// Debe invocarse antes de las lecturas que deciden la escritura.
    /// No admite alcances ni transacciones anidados.
    /// </summary>
    Task<IClinicalWriteScope> BeginAsync(
        CancellationToken cancellationToken = default);
}
