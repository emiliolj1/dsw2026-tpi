/*
 N04 - Auditoría conservadora, SOLO LECTURA.
 Ejecutar en SSMS/ADS después de SELECCIONAR MANUALMENTE una COPIA restaurada.
 No cambia tablas, no crea temporales persistentes y no muestra PII.
 Ejecutar ANTES y DESPUÉS de la migración sobre una copia AUTORIZADA.
 NO autoriza correr RebuildIdentityWithGuidKeys sobre bases legadas.
*/
SET NOCOUNT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 51004, 'N04: seleccionar manualmente una base de prueba/restaurada.', 1;

SELECT DB_NAME() AS BaseSeleccionada,
       @@SERVERNAME AS Servidor,
       SYSDATETIMEOFFSET() AS FechaAuditoria;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    SELECT N'ALERTA: no existe __EFMigrationsHistory; revisar esquema antes de ejecutar migraciones.' AS Advertencia;
ELSE
    SELECT MigrationId, ProductVersion
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId;

-- Los nombres actuales de Identity son Users, Roles y UsersRoles (no AspNetUsers).
-- El inventario dinámico solo utiliza nombres reales del catálogo y QUOTENAME.
DECLARE @conteos nvarchar(max) = N'';
SELECT @conteos = @conteos +
    CASE WHEN LEN(@conteos) > 0 THEN N' UNION ALL ' ELSE N'' END +
    N'SELECT N''' + REPLACE(t.name, '''', '''''') +
    N''' AS Tabla, COUNT_BIG(*) AS Filas FROM dbo.' + QUOTENAME(t.name)
FROM sys.tables AS t
WHERE t.schema_id = SCHEMA_ID(N'dbo')
  AND t.name IN (
      N'Specialities', N'Doctors', N'Patients', N'Availabilities',
      N'AvailabilityRules', N'Appointments', N'Users', N'Roles',
      N'UsersRoles', N'ApplicationUsers', N'AspNetUsers', N'AspNetRoles');

IF LEN(@conteos) > 0
    EXEC sp_executesql @conteos;
ELSE
    SELECT N'ALERTA: no se encontraron tablas clínicas reconocidas.' AS Advertencia;

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL
BEGIN
    EXEC(N'SELECT Status, COUNT_BIG(*) AS Cantidad
        FROM dbo.Appointments GROUP BY Status ORDER BY Status');
END

IF OBJECT_ID(N'dbo.Availabilities', N'U') IS NOT NULL
BEGIN
    EXEC(N'SELECT Status, COUNT_BIG(*) AS Cantidad
        FROM dbo.Availabilities GROUP BY Status ORDER BY Status');

    IF COL_LENGTH(N'dbo.Availabilities', N'AvailabilityRuleId') IS NULL
        SELECT N'ESQUEMA PRE N01: AvailabilityRuleId todavía no existe.' AS Observacion;
    ELSE
    BEGIN
        EXEC(N'SELECT COUNT_BIG(*) AS SlotsLegadosSinRegla
            FROM dbo.Availabilities WHERE AvailabilityRuleId IS NULL');

        IF OBJECT_ID(N'dbo.AvailabilityRules', N'U') IS NOT NULL
            EXEC(N'SELECT COUNT_BIG(*) AS SlotsConReglaInexistente
                FROM dbo.Availabilities s
                LEFT JOIN dbo.AvailabilityRules r ON r.Id = s.AvailabilityRuleId
                WHERE s.AvailabilityRuleId IS NOT NULL AND r.Id IS NULL');
    END
END

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Availabilities', N'U') IS NOT NULL
BEGIN
    EXEC(N'SELECT COUNT_BIG(*) AS CitasSinSlot
        FROM dbo.Appointments a
        LEFT JOIN dbo.Availabilities s ON s.Id = a.AvailabilityId
        WHERE s.Id IS NULL');

    EXEC(N'SELECT COUNT_BIG(*) AS ReservasActivasConSlotNoReservadoOEliminado
        FROM dbo.Appointments a
        JOIN dbo.Availabilities s ON s.Id = a.AvailabilityId
        WHERE a.Status = ''BOOKED'' AND a.Deleted = 0
          AND (s.Status <> ''Booked'' OR s.Deleted = 1)');
END

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Patients', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Doctors', N'U') IS NOT NULL
BEGIN
    EXEC(N'SELECT COUNT_BIG(*) AS CitasSinPaciente
        FROM dbo.Appointments a LEFT JOIN dbo.Patients p ON p.Id = a.PatientId
        WHERE p.Id IS NULL');
    EXEC(N'SELECT COUNT_BIG(*) AS CitasSinDoctor
        FROM dbo.Appointments a LEFT JOIN dbo.Doctors d ON d.Id = a.DoctorId
        WHERE d.Id IS NULL');
END

IF OBJECT_ID(N'dbo.Patients', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.Patients', N'Dni') IS NOT NULL
        EXEC(N'SELECT COUNT_BIG(*) AS DNIDuplicados
            FROM (SELECT Dni FROM dbo.Patients GROUP BY Dni HAVING COUNT_BIG(*) > 1) d');

    IF COL_LENGTH(N'dbo.Patients', N'UserId') IS NULL
        SELECT N'ESQUEMA LEGADO: Patients.UserId todavía no existe; no migrar automáticamente.' AS Observacion;
    ELSE
    BEGIN
        EXEC(N'SELECT COUNT_BIG(*) AS UserIdCero
            FROM dbo.Patients
            WHERE UserId = ''00000000-0000-0000-0000-000000000000''');

        EXEC(N'SELECT COUNT_BIG(*) AS UserIdDuplicados
            FROM (SELECT UserId FROM dbo.Patients GROUP BY UserId HAVING COUNT_BIG(*) > 1) d');

        IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.Users', N'Id') IS NOT NULL
        BEGIN
            -- Esquemas GUID actuales: nunca intentar la comparación si Users.Id es nvarchar.
            IF EXISTS (SELECT 1 FROM sys.columns c
                       JOIN sys.types ty ON c.user_type_id = ty.user_type_id
                       WHERE c.object_id = OBJECT_ID(N'dbo.Users')
                         AND c.name = N'Id' AND ty.name = N'uniqueidentifier')
                EXEC(N'SELECT COUNT_BIG(*) AS PacientesSinUsuarioIdentity
                    FROM dbo.Patients p
                    LEFT JOIN dbo.Users u ON u.Id = p.UserId
                    WHERE u.Id IS NULL');
            ELSE
                SELECT N'ALERTA: Users.Id no es GUID; hay que revisar la migración de identidad.' AS Observacion;
        END
    END
END

IF OBJECT_ID(N'dbo.Availabilities', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Doctors', N'U') IS NOT NULL
    EXEC(N'SELECT COUNT_BIG(*) AS SlotsSinDoctor
        FROM dbo.Availabilities s LEFT JOIN dbo.Doctors d ON d.Id = s.DoctorId
        WHERE d.Id IS NULL');

SELECT N'N04: comparar ambos informes sobre COPIA; conteos iguales NO acreditan por si solos IDs, login o restauración.' AS NotaFinal;
