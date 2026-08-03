using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Patient : EntityBase
{
    public Guid UserId { get; private set; }
    public long Dni { get; private set; }
    public string? FullName { get; private set; }
    private Patient()
    {
    }
    public Patient(
        Guid userId,
        long dni,
        string? fullName = null,
        Guid? id = null) : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException(
                "El identificador del usuario es obligatorio.",
                nameof(userId));
        }

        var normalizedFullName = fullName?.Trim();

        if (normalizedFullName is { Length: > 150 })
        {
            throw new ArgumentException(
                "El nombre completo no puede superar los 150 caracteres.",
                nameof(fullName));
        }

        UserId = userId;
        Dni = dni;
        FullName = normalizedFullName;
    }
}
