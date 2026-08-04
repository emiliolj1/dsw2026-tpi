namespace Dsw2026Tpi.CrossCutting.Exceptions;

/// <summary>
/// Excepción lanzada cuando una operación produce un conflicto.
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message, string errorCode)
        : base(message, errorCode)
    {
    }
}
