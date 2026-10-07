using Dsw2026Tpi.CrossCutting.Exceptions;
using Dsw2026Tpi.Data.Options;
using Dsw2026Tpi.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Data;
using System.Transactions;

namespace Dsw2026Tpi.Data.Transactions;

public sealed class ClinicalWriteScopeFactory
    : IClinicalWriteScopeFactory
{
    public const string LockResource = "tpi:clinical-write";

    private readonly Dsw2026TpiDbContext _context;
    private readonly int _lockTimeoutMilliseconds;

    private int _scopeActive;

    public ClinicalWriteScopeFactory(
        Dsw2026TpiDbContext context,
        IOptions<ClinicalWriteOptions> options)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(options);

        _context = context;
        _lockTimeoutMilliseconds =
            options.Value.LockTimeoutMilliseconds;

        if (_lockTimeoutMilliseconds is < 0 or > 60000)
        {
            throw new OptionsValidationException(
                ClinicalWriteOptions.SectionName,
                typeof(ClinicalWriteOptions),
                [
                    "ClinicalWrite:LockTimeoutMilliseconds " +
                    "debe estar entre 0 y 60000."
                ]);
        }
    }

    public async Task<IClinicalWriteScope> BeginAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Interlocked.CompareExchange(
                ref _scopeActive, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Ya existe un alcance de escritura activo " +
                "en esta solicitud.");
        }

        IDbContextTransaction? transaction = null;

        try
        {
            if (!_context.Database.IsSqlServer())
            {
                throw new InvalidOperationException(
                    "El alcance de escritura clínica requiere SQL Server.");
            }

            if (_context.Database.CurrentTransaction is not null ||
                System.Transactions.Transaction.Current is not null ||
                _context.Database.GetEnlistedTransaction() is not null)
            {
                throw new InvalidOperationException(
                    "No se permiten transacciones anidadas.");
            }

            if (_context.ChangeTracker.HasChanges())
            {
                throw new InvalidOperationException(
                    "El alcance debe iniciarse antes de modificar entidades.");
            }

            transaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.ReadCommitted,
                cancellationToken);

            await AcquireLockAsync(transaction, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Evita reutilizar entidades rastreadas antes del bloqueo.
            // Las lecturas decisorias deben realizarse dentro del alcance.
            _context.ChangeTracker.Clear();

            return new ClinicalWriteScope(
                _context,
                transaction,
                () =>
                {
                    Interlocked.Exchange(ref _scopeActive, 0);
                });
        }
        catch
        {
            try
            {
                if (transaction is not null)
                {
                    await RollbackAndDisposeAsync(transaction);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _scopeActive, 0);
            }

            throw;
        }
    }

    private async Task AcquireLockAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command =
            _context.Database.GetDbConnection().CreateCommand();

        command.Transaction = transaction.GetDbTransaction();

        // El timeout del comando debe permitir que SQL devuelva
        // el resultado del timeout específico del bloqueo.
        command.CommandTimeout = Math.Max(
            30,
            (_lockTimeoutMilliseconds + 999) / 1000 + 5);

        command.CommandText = """
            DECLARE @result int;

            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = @timeout,
                @DbPrincipal = N'public';

            SELECT @result;
            """;

        var resourceParameter = command.CreateParameter();
        resourceParameter.ParameterName = "@resource";
        resourceParameter.DbType = DbType.String;
        resourceParameter.Size = 255;
        resourceParameter.Value = LockResource;
        command.Parameters.Add(resourceParameter);

        var timeoutParameter = command.CreateParameter();
        timeoutParameter.ParameterName = "@timeout";
        timeoutParameter.DbType = DbType.Int32;
        timeoutParameter.Value = _lockTimeoutMilliseconds;
        command.Parameters.Add(timeoutParameter);

        var result = await command.ExecuteScalarAsync(
            cancellationToken);

        if (result is not int returnCode)
        {
            throw new InvalidOperationException(
                "SQL Server no devolvió un resultado válido " +
                "al adquirir el bloqueo clínico.");
        }

        if (returnCode >= 0)
        {
            return;
        }

        switch (returnCode)
        {
            case -1:
            case -3:
                throw new ClinicalWriteConflictException();

            case -2:
                cancellationToken.ThrowIfCancellationRequested();

                throw new InvalidOperationException(
                    "SQL Server canceló la adquisición del bloqueo " +
                    "sin una cancelación del token de la operación.");

            default:
                throw new InvalidOperationException(
                    "Falló la adquisición del bloqueo clínico. " +
                    $"Código devuelto: {returnCode}.");
        }
    }

    private static async Task RollbackAndDisposeAsync(
        IDbContextTransaction transaction)
    {
        try
        {
            // La limpieza debe ejecutarse aunque la solicitud se canceló.
            await transaction.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private sealed class ClinicalWriteScope : IClinicalWriteScope
    {
        private readonly Dsw2026TpiDbContext _context;
        private readonly IDbContextTransaction _transaction;
        private readonly Action _releaseScope;

        private bool _completionAttempted;
        private bool _completed;
        private bool _disposed;

        public ClinicalWriteScope(
            Dsw2026TpiDbContext context,
            IDbContextTransaction transaction,
            Action releaseScope)
        {
            _context = context;
            _transaction = transaction;
            _releaseScope = releaseScope;
        }

        public async Task CompleteAsync(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_completionAttempted)
            {
                throw new InvalidOperationException(
                    "CompleteAsync solo puede ejecutarse una vez " +
                    "por alcance.");
            }

            _completionAttempted = true;

            if (!ReferenceEquals(
                    _context.Database.CurrentTransaction,
                    _transaction))
            {
                throw new InvalidOperationException(
                    "La transacción del alcance fue reemplazada " +
                    "o finalizada fuera de él.");
            }

            await _context.SaveChangesAsync(cancellationToken);
            await _transaction.CommitAsync(cancellationToken);

            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                if (_completed)
                {
                    await _transaction.DisposeAsync();
                }
                else
                {
                    await RollbackAndDisposeAsync(_transaction);
                }
            }
            finally
            {
                try
                {
                    if (!_completed)
                    {
                        // No conservar en memoria estados que se revirtieron.
                        _context.ChangeTracker.Clear();
                    }
                }
                finally
                {
                    _releaseScope();
                }
            }
        }
    }
}
