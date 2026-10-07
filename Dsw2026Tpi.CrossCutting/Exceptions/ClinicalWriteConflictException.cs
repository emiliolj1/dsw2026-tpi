namespace Dsw2026Tpi.CrossCutting.Exceptions;

public sealed class ClinicalWriteConflictException : ConflictException
{
    public const string Code = "CLINICAL_WRITE_CONFLICT";

    public ClinicalWriteConflictException()
        : base(
            "Otra operación está modificando los datos clínicos. " +
            "Intentá nuevamente.",
            Code)
    {
    }
}
