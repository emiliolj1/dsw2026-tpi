# N04 — Validación de migraciones y preservación de datos

**Estado del entregable:** PR #34 integrado y validación local ejecutada por Emilio
el 2026-11-11 en `af360f1`. Se comprobaron la preservación sobre una copia
restaurada con datos sintéticos, la regresión y el acceso por API a la reserva
preexistente. Resultados: [evidencia-migraciones.md](evidencia-migraciones.md).
La revisión del equipo y la aceptación integrada C04 se registran por separado.

## Dependencias verificadas en GitHub (2026-11-07)

- E01: PR #27 fusionado en `development`.
- N03: PR #31 fusionado en `development`.
- L03: PR #32 fusionado en `development`.
- L04: PR #33 también fusionado (no es dependencia formal de N04).

## Lo que ya existe y NO hay que regenerar

- `20261025232313_AddMonthlyAvailabilityRules`: migración aditiva.
- `Availabilities.AvailabilityRuleId`: GUID nullable, FK hacia `AvailabilityRules` sin borrado en cascada.
- `scripts/migrations/preflight.sql`: diagnóstico previo.
- `docs/backend/migraciones.md` y `docs/backend/evidencia-migraciones.md`:
  estrategia y plantilla de aceptación.

**Importante:** las migraciones antiguas `RebuildIdentityWithGuidKeys` y
`LinkPatientToIdentityUser` NO son automáticamente seguras para datos legados:
una elimina/recrea tablas de usuarios y la otra elimina columnas de email y
agrega `Patients.UserId`. No ejecutar `dotnet ef database update` en una base
legada sin respaldo, inventario, exportación validada y decisión del equipo.

## Cambios de este paquete

- `Dsw2026Tpi.Tests/Integration/MigrationPreservationSqlTests.cs`:
  crea una base SQL exclusiva con GUID, migra Identity a su esquema actual,
  crea un usuario y su relación de rol de prueba, deja el esquema de aplicación
  en la migración anterior a N01, inserta paciente/médico/slot BOOKED/cita BOOKED,
  aplica N01 y comprueba IDs, estados, relaciones, rowversion, FK opcional
  y ausencia de cascada. Verifica también que repetir `MigrateAsync` no cambia
  los registros. Borra únicamente el catálogo de prueba generado.
- `scripts/migrations/n04-auditoria.sql`: reporte de solo lectura (conteos y
  anomalías agregadas) para ejecutar en una **copia restaurada** antes/después.
  Reconoce los nombres reales `Users`, `Roles` y `UsersRoles` de Identity.

Esta prueba cubre el paso desde el **esquema GUID ya normalizado previo a N01**.
No ensaya ni certifica una conversión de una base histórica de Identity con IDs
string a GUID, ni demuestra que una base particular fue respaldada/restaurada.

## Ejecutar pruebas SQL en Windows / Git Bash

Requisitos: SDK .NET 10, SQL Server o LocalDB de pruebas y credenciales que
permitan crear/eliminar una **base nueva y aislada**. No usar SQL productivo.

Ejemplo para LocalDB (Git Bash, Windows; adaptar a tu instalación):

```bash
export DSW2026_TEST_SQL_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=Dsw2026Tpi_Test_Config;Trusted_Connection=True;TrustServerCertificate=True'

dotnet restore Dsw2026Tpi.slnx
dotnet build Dsw2026Tpi.slnx --no-restore

dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj \
  --no-build --filter 'FullyQualifiedName~MigrationPreservationSqlTests' \
  --logger 'console;verbosity=normal'

dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj \
  --no-build --filter 'Category=SqlServer' \
  --logger 'console;verbosity=normal'
```

El fixture del proyecto genera bases `Dsw2026Tpi_Test_<GUID>` y las elimina al
terminar. La prueba N04 aplica el mismo control de nombre al limpiarse.

## Validación de una base con datos (pasos manuales)

1. **Identificar el escenario:** base nueva, esquema GUID actual con datos,
   o legado de identidad. Registrar servidor, nombre del catálogo, SHA y versión.
2. **Conservar el origen:** obtener respaldo y comprobar restauración sobre otro
   catálogo. No convertir sobre el origen ni sobre una base compartida.
3. **En la copia** ejecutar primero `preflight.sql` y
   `n04-auditoria.sql`; registrar resultados agregados, sin PII ni secretos.
4. **Si es base legada** anterior a `RebuildIdentityWithGuidKeys` o
   `LinkPatientToIdentityUser`: detener; coordinar con Emilio/Charly/Lucas la
   exportación y el mapeo verificable, incluidos roles, pacientes y usuarios.
   No inventar enlaces ni rellenar `UserId` con GUID vacío.
5. **Si es base ya normalizada y respaldada**, revisar `dotnet ef migrations list`
   para ambos contextos. Aplicar migraciones faltantes únicamente al catálogo
   de prueba, con `--context`, `--project`, `--startup-project` y cadena de
   conexión apuntando explícitamente a **esa copia**. No usar configuraciones
   implícitas de desarrollo/producción.
6. Repetir `n04-auditoria.sql`; comparar migraciones, inventario, conteos y
   anomalías. Verificar por separado igualdad de IDs y FK, estados BOOKED,
   acceso a históricos, login real y posibilidad de restauración. Conteos solos
   no prueban igualdad de registros.
7. Registrar los comandos efectivamente ejecutados y sus resultados en
   `docs/backend/evidencia-migraciones.md` (sin credenciales, usuarios reales,
   tokens ni datos clínicos). Obtener revisión de Emilio, Lucas y Charly.

**No se agrega migración nueva:** las verificaciones no exigen cambios de modelo.
Si otra tarea introduce un cambio de esquema, revisarlo y generar una migración
N04 adicional solo después de acordar el modelo y la estrategia de respaldo.

## Resultado de la validación local

- Compilación correcta en Windows con .NET 10 durante `dotnet test`.
- `MigrationPreservationSqlTests` aprobada sobre SQL Server LocalDB.
- Regresión completa: 136 pruebas sin SQL y 90 SQL aprobadas.
- Copia restaurada con datos sintéticos: inventario, IDs, estados y vínculos
  preservados al aplicar N01.
- Login real del paciente y consulta de la reserva preexistente: HTTP 200.
- Implementación integrada mediante PR #34, merge `af360f1`.
- Evidencia integrada mediante [PR #35](https://github.com/emiliolj1/dsw2026-tpi/pull/35),
  merge `d14590d`.

La prueba se realizó sobre Identity ya normalizado a GUID. Cualquier
conversión de bases legadas requiere un procedimiento validado aparte.
