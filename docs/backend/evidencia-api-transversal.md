# Evidencia de API transversal — E02

## Entorno y estado

- Responsable: Emilio.
- Fecha de verificación: 2026-10-25.
- Equipo: PC02; Windows y Git Bash.
- SDK de referencia informado al iniciar E01: 10.0.401.
- Rama local: fix/emilio-api-transversal.
- Base: a9d3930ef23846c1b9f761165e116f65b8f41551.
- Motor SQL: LocalDB, instancia MSSQLLocalDB.
- Estado: implementación local verificada; pendiente de PR e integración.
- No hay todavía número de PR ni SHA integrado para E02.

## Regresión ejecutada por Emilio

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Category!=SqlServer | 116 | 0 | 0 |
| Category=SqlServer | 62 | 0 | 0 |
| Total de ambas ejecuciones | 178 | 0 | 0 |

Las categorías se ejecutaron por separado.
La compilación final no reportó advertencias.

Casos específicos incorporados en E02:

| Suite | Casos | Categoría |
|---|---:|---|
| CorsSqlTests | 4 | SqlServer |
| ApiBoundarySqlTests | 7 | SqlServer |
| RateLimitingSqlTests | 5 | SqlServer |
| ExceptionHandlingMiddlewareTests | 4 | No SQL |

Estos 20 casos están incluidos en los totales de regresión.
Se reforzó además PatientLoginRollbackSqlTests, sin agregar otro caso.

Comandos de regresión:

```bash
dotnet build --no-restore --nologo
dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj --no-build --filter "Category!=SqlServer" --nologo --verbosity minimal
dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj --no-build --filter "Category=SqlServer" --nologo --verbosity minimal
```

La variable DSW2026_TEST_SQL_CONNECTION debe estar configurada
en la terminal que ejecuta las pruebas SQL, conforme a pruebas-sql.md.

## CORS y errores HTTP

- Origen permitido: http://localhost:5173.
- Configuración explícita y validada; no se utiliza AllowAnyOrigin.
- Preflight HTTP 204 con los headers necesarios para Authorization
  y Content-Type desde el origen permitido.
- Origen no permitido sin Access-Control-Allow-Origin.
- Respuestas de error con application/json y formato común:
  400, 401, 403, 404 de entidad, 404 de ruta, 405 y 415.
- Los conflictos 409 de catálogos conservan sus códigos de negocio;
  se incluyen en la regresión SQL existente.
- Un fallo interno no previsto devuelve 500 con UNHANDLED_ERROR
  y sin revelar el mensaje original.
- Se conservan las garantías de rollback del login de pacientes.
- Una respuesta iniciada no se limpia ni se reescribe.
- La cancelación del cliente no se convierte en un 500 artificial.

## Rate limiting y logs

- AdminLogin: 5 por IP en 60 segundos.
- PatientLogin: 10 por IP en 60 segundos.
- Booking: 5 por usuario autenticado en 60 segundos.
- General: 100 por usuario o IP en 60 segundos.
- QueueLimit igual a cero.
- El exceso devuelve 429, JSON común y RATE_LIMIT_EXCEEDED.
- Se verificó aislamiento entre IP y entre usuarios según la política.
- El header para sustituir la IP se incorpora solo al host de pruebas.
  No se habilitó confianza en ese header en la API real.
- Los rechazos registran ruta, partición y TraceId.
- El middleware de excepciones registra método, ruta sin query,
  tipo de excepción y TraceId, sin mensaje crudo, payload ni JWT.

## Dependencias

La dependencia original era:

Microsoft.EntityFrameworkCore.SqlServer 10.0.9 ->
Microsoft.Data.SqlClient 6.1.1 ->
System.Security.Cryptography.Pkcs 9.0.4.

Se agregó una referencia explícita a Pkcs 9.0.21 en Data.
dotnet nuget why confirmó la resolución 9.0.21 tanto por la referencia
directa como por la cadena transitiva.
Se conservaron EF Core 10.0.9 y SqlClient 6.1.1.

Avisos que originaban NU1902 y NU1903:
- [GHSA-2fmv-rv2c-59x7 / CVE-2026-70162](https://github.com/advisories/GHSA-2fmv-rv2c-59x7).
- [GHSA-846f-f764-76mx / CVE-2026-84008](https://github.com/advisories/GHSA-846f-f764-76mx).

Ambos avisos identifican 9.0.21 como una versión corregida de la rama 9.

Consultas ejecutadas el 2026-10-25:

```bash
dotnet list Dsw2026Tpi.Data/Dsw2026Tpi.Data.csproj package --vulnerable --include-transitive --format json
dotnet list Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj package --vulnerable --include-transitive --format json
```

Los JSON devueltos identifican los proyectos y no listan paquetes
vulnerables. La fuente remota informada es https://api.nuget.org/v3/index.json.
Este resultado corresponde a esos proyectos y sus dependencias transitivas,
en esa fecha y con los avisos disponibles; no acredita ausencia permanente
de vulnerabilidades ni constituye una auditoría de todos los componentes.

## Comprobación manual de arranque y navegador

En PC02 se detectó que no existía ninguna base Dsw2026Tpi
en la instancia LocalDB consultada.
El arranque fallaba con SQL 4060 durante la inicialización de roles.

Se aplicaron las migraciones existentes, primero AuthenticationDbContext
y luego Dsw2026TpiDbContext, sobre la nueva base local Dsw2026Tpi.
Ambos comandos finalizaron con Done.
No se generaron migraciones, snapshots ni cambios de esquema en Git.

Después se verificó:
- Arranque real de la API en http://localhost:5278.
- Carga de /swagger/index.html en Development.
- Login administrativo por Swagger y obtención de JWT.
- Desde una página temporal en http://localhost:5173, fetch autenticado
  a /health-check: la consola mostró HTTP 200 Healthy.
- Desde otra página temporal en http://localhost:5174, el navegador
  rechazó el preflight por ausencia de Access-Control-Allow-Origin.
  La consola mostró el error CORS y Failed to fetch.

Las páginas se sirvieron con Node sin incorporar archivos al repositorio.
Los JWT y contraseñas utilizados no forman parte de esta evidencia.
El HTTP 204 de OPTIONS fue verificado en los tests SQL;
las capturas manuales corresponden a la consola del navegador.

## Calendario y límites pendientes

El calendario nacional 2026 acordado, sus fuentes y responsable están
en [calendario-operativo.md](calendario-operativo.md).
Debe contrastarse la lista documentada con NonWorkingDays:Dates
en el diff final de configuración.

Falta confirmar cierres adicionales propios del centro y verificar
la exclusión de fechas en la planificación mensual de N02.
Se debe preparar el calendario 2027 antes de operar en ese año.

La carga de Swagger no acredita una revisión completa de todos
los esquemas y ejemplos de los módulos todavía pendientes.
E02 no acredita la aceptación integrada ni la medición de rendimiento C04.

Antes del commit se debe revisar el diff completo y ejecutar
git diff --check. Después corresponde PR hacia development y revisión;
el número y SHA se registran cuando existan.
