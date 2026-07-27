using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Patient : EntityBase
{
    public string Email { get; private set; }
    public string NormalizedEmail { get; private set; }
    public long Dni { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private Patient()
    {
    }
#pragma warning restore CS8618
    #endregion

    public Patient(string email, long dni, Guid? id = null) : base(id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Email = email.Trim();
        NormalizedEmail = NormalizeEmail(email);
        Dni = dni;
    }

    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToUpperInvariant();
    }
}