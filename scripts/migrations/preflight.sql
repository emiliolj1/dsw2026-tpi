/*
 N04 — Preflight SOLO LECTURA. Ejecutar desde SSMS/ADS SOBRE COPIA/DESTINO
 explícitamente seleccionado. Nunca elige ni crea una base automáticamente.
 NO ejecutar migrations update antes de interpretar advertencias de Identity.
 El script no devuelve DNI, nombres, emails, hashes ni otros datos personales.
*/
SET NOCOUNT ON;
SELECT DB_NAME() AS DatabaseName, @@SERVERNAME AS ServerName,
       SYSDATETIMEOFFSET() AS ObservedAt;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
BEGIN
  SELECT MigrationId, ProductVersion
  FROM dbo.__EFMigrationsHistory
  ORDER BY MigrationId;
END
ELSE
  SELECT N'No existe __EFMigrationsHistory; verificar esquema antes de migrar' AS Warning;

SELECT t.name AS TableName
FROM sys.tables t
WHERE t.name IN
 (N'Doctors', N'Specialities', N'Patients', N'Availabilities',
  N'AvailabilityRules', N'Appointments', N'AspNetUsers',
  N'AspNetRoles', N'AspNetUserRoles')
ORDER BY t.name;

DECLARE @sql nvarchar(max) = N'';
SELECT @sql = @sql +
  N'SELECT N''' + t.name + N''' AS TableName, COUNT_BIG(*) AS RowsCount FROM dbo.' +
  QUOTENAME(t.name) + N' UNION ALL '
FROM sys.tables t
WHERE t.schema_id = SCHEMA_ID(N'dbo')
  AND t.name IN
  (N'Doctors', N'Specialities', N'Patients', N'Availabilities',
   N'AvailabilityRules', N'Appointments', N'AspNetUsers',
   N'AspNetRoles', N'AspNetUserRoles');
IF LEN(@sql) > 0
BEGIN
  SET @sql = LEFT(@sql, LEN(@sql) - LEN(N' UNION ALL '));
  EXEC sp_executesql @sql;
END

IF OBJECT_ID(N'dbo.Patients', N'U') IS NOT NULL
BEGIN
    SELECT c.name AS PatientColumn, ty.name AS SqlType, c.is_nullable
    FROM sys.columns c
    JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE c.object_id = OBJECT_ID(N'dbo.Patients')
      AND c.name IN (N'Id', N'UserId', N'Dni', N'Email', N'NormalizedEmail');

    IF COL_LENGTH(N'dbo.Patients', N'UserId') IS NOT NULL
    BEGIN
      EXEC(N'SELECT COUNT_BIG(*) AS DuplicateUserIdGroups
          FROM (SELECT UserId FROM dbo.Patients GROUP BY UserId HAVING COUNT(*) > 1) g');
      EXEC(N'SELECT COUNT_BIG(*) AS EmptyUserIds FROM dbo.Patients
          WHERE UserId = CONVERT(uniqueidentifier, ''00000000-0000-0000-0000-000000000000'')');
    END
END

IF OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.Availabilities', N'U') IS NOT NULL
BEGIN
  EXEC(N'SELECT COUNT_BIG(*) AS OrphanAppointmentsMissingSlot
    FROM dbo.Appointments a
    LEFT JOIN dbo.Availabilities s ON s.Id = a.AvailabilityId
    WHERE s.Id IS NULL');
END

SELECT N'Solo diagnóstico: conservar origen intacto; no ejecutar Identity destructiva sin exportación validada.' AS Notice;
