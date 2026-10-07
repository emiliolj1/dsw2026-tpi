namespace Dsw2026Tpi.Data.Options;

public sealed class ClinicalWriteOptions
{
    public const string SectionName = "ClinicalWrite";

    /// <summary>
    /// Tiempo máximo de espera para adquirir el bloqueo SQL.
    /// Cero significa que no se espera si está ocupado.
    /// </summary>
    public int LockTimeoutMilliseconds { get; set; } = 5000;
}