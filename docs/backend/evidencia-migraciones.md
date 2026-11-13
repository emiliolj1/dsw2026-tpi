# Evidencia de migraciones — N04

Validación local ejecutada por Emilio el 2026-11-11, sobre `af360f132be362b204b2f964a77f1498755b9890` (`test/emilio-validacion-n04`, integración del PR #34).

- SDK .NET 10.0.401; runtime de pruebas .NET 10.0.12.
- SQL Server LocalDB `MSSQLLocalDB`, versión 15.0.4382.1.
- Escenario: esquema de Identity con GUID y esquema clínico anterior a N01, con datos sintéticos.
- Origen sintético: `Dsw2026Tpi_Test_N04_Origen_20261111014022_9172`.
- Copia restaurada: `Dsw2026Tpi_Test_N04_Copia_20261111014022_9172`.
- La base de desarrollo no contenía reservas aptas para el escenario y no fue destino de la migración.

## Pruebas en af360f1

| Ejecución | Aprobadas | Fallidas | Omitidas | TRX local |
|---|---:|---:|---:|---|
| MigrationPreservationSqlTests | 1 | 0 | 0 | `n04-preservacion.trx` |
| Regresión sin SQL | 136 | 0 | 0 | `n04-regresion-no-sql.trx` |
| Regresión con SQL Server | 90 | 0 | 0 | `n04-regresion-sql.trx` |

Regresión: 226 aprobadas. El caso específico ya está incluido en las 90 pruebas SQL. La compilación de la prueba específica tuvo una advertencia `xUnit2031` en `AppointmentConcurrencySqlTests.cs`.

## Restauración y preservación

Se creó un respaldo `COPY_ONLY` con checksum del catálogo sintético y se restauró en otro catálogo con archivos de datos y log propios. Se aplicó `20261025232313_AddMonthlyAvailabilityRules` al contexto `Dsw2026TpiDbContext` de la copia, y se verificó el cambio en el historial de migraciones.

| Control de la reserva sintética | Resultado antes y después |
|---|---|
| Cita | `1a7d7044-60ee-415e-a1f2-7d304f0b1e07`, `BOOKED`, no eliminada |
| Turno | `aa42ae33-cde0-4011-8e74-b1d495f0f98d`, `Booked`, no eliminado, fecha `2026-11-18` |
| Paciente y usuario Identity | `5a92e27a-5c7b-4db0-b5cb-6b1539afe532` → `d9671e77-64e0-48f2-9456-48ee6ece85c6` |
| Médico de cita y turno | `6164e71a-784e-4523-95ff-90de35e92762` |
| Rol vinculado | `Paciente`, `44788885-062b-4f29-9471-bfea3e2e1984` |
| RowVersion | `0x00000000000007D1` en ambas lecturas |
| AvailabilityRuleId | Columna ausente antes de N01; `NULL` para el turno previo después de N01 |

## Inventario en la copia, antes de iniciar la API

Comparación de los informes de `scripts/migrations/n04-auditoria.sql`.

| Indicador | Antes | Después |
|---|---:|---:|
| Citas (`Appointments`) | 1 | 1 |
| Turnos (`Availabilities`) | 1 | 1 |
| Médicos (`Doctors`) | 1 | 1 |
| Pacientes (`Patients`) | 1 | 1 |
| Especialidades (`Specialities`) | 1 | 1 |
| Usuarios Identity (`Users`) | 1 | 1 |
| Roles (`Roles`) | 2 | 2 |
| Vínculos usuario/rol (`UsersRoles`) | 1 | 1 |
| Reglas (`AvailabilityRules`) | Tabla ausente | 0 |
| Turnos legados sin regla | Columna ausente | 1 |
| Citas sin turno | 0 | 0 |
| Reservas activas con turno no reservado o eliminado | 0 | 0 |
| DNI duplicados | 0 | 0 |
| UserId duplicados en pacientes | 0 | 0 |
| Pacientes sin usuario Identity | 0 | 0 |

La prueba automatizada verificó también que la FK de `AvailabilityRuleId` no
borra en cascada y que volver a ejecutar la migración no altera el registro.
Los informes de consola se conservaron localmente como `n04-antes.SOIYZO`
y `n04-despues.GqE7AV`.

## Comprobación por HTTP

La API se inició contra la copia migrada. `POST /api/auth/patient/login` respondió 200 y devolvió un JWT. Con ese JWT, `GET /api/appointments/patient` respondió 200 y recuperó la reserva `BOOKED` con los mismos identificadores de cita y turno. Los JWT, secretos, archivos de respaldo y TRX permanecen fuera de Git.

El escenario no valida una conversión histórica de Identity desde claves string a GUID ni acredita la aceptación integrada C04. La evidencia se incorporó a `development` mediante el [PR #35](https://github.com/emiliolj1/dsw2026-tpi/pull/35) (`d14590d`).
