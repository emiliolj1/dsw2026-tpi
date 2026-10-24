# Evidencia de autenticación — E01

## Entorno y estado

- Responsable: Emilio.
- Fecha: 2026-10-24.
- Rama: fix/emilio-autenticacion-cierre.
- Base: 82e2f0e.
- SDK .NET: 10.0.401.
- Entorno: Windows y Git Bash.
- Motor utilizado: SQL Server LocalDB, instancia MSSQLLocalDB.
- Infraestructura: C01, con bases exclusivas de pruebas.
- Estado: implementación local verificada; pendiente de PR e integración.

## Cambios

- Login de pacientes dentro del alcance SQL compartido de G02.
- Identity y dominio participan en la misma conexión y transacción.
- Las lecturas decisorias se realizan después de adquirir el bloqueo.
- El alta confirma identidad, rol y paciente conjuntamente.
- Se eliminan las compensaciones que borraban identidades.
- Se rechazan combinaciones incompatibles de email/DNI y bajas lógicas.
- El seeder conserva usuarios, roles y asignaciones al repetirse.
- Credenciales iniciales incompletas producen un diagnóstico explícito.
- El seeder rechaza identidades incompatibles, eliminadas o sin contraseña.
- La carga de roles utiliza Path.Combine y contempla seeding síncrono
  y asíncrono.
- La configuración JWT se valida al iniciar la API.
- Jwt:Key requiere al menos 32 bytes UTF-8; issuer y audience son
  obligatorios; ExpiresInMinutes debe ser positivo.
- Emisión JWT y revocación utilizan TimeProvider.
- La validación JWT utiliza ClockSkew igual a cero.
- Las entradas de revocación vencidas se limpian al utilizar el servicio.
- Logout devuelve HTTP 200, text/plain y cuerpo literal ok.
- Se actualizan los metadatos de autenticación en Swagger.

## Resultados ejecutados por Emilio

| Suite | Aprobadas | Fallidas | Omitidas |
|---|---:|---:|---:|
| Category!=SqlServer | 112 | 0 | 0 |
| Category=SqlServer | 46 | 0 | 0 |

Ambas categorías se ejecutaron por separado.
Estos totales incluyen las pruebas específicas descritas a continuación;
no deben sumarse nuevamente.

Casos específicos de E01:
- AuthenticationSqlTests: 5.
- IdentitySeederSqlTests: 7.
- PatientLoginConcurrencySqlTests: 3.
- PatientLoginRollbackSqlTests: 1.
- JwtLifetimeSqlTests: 2.
- JwtServiceTests: 8.
- TokenRevocationServiceTests: 7.

git diff --check no informó problemas.

## Verificaciones principales

- Login administrativo correcto e incorrecto.
- Acceso autenticado, logout y rechazo posterior del token utilizado.
- Un segundo token del mismo administrador permanece válido.
- Autoalta y reingreso del paciente sin duplicados.
- Rechazo de email/DNI cruzados e identidad administrativa como paciente.
- Rechazo de usuario o paciente eliminado, sin recreación ni reactivación.
- Primeros accesos concurrentes compatibles: dos respuestas 200,
  una identidad y un paciente.
- Primeros accesos concurrentes incompatibles: un 200 y un 401,
  sin identidades sobrantes.
- Fallo artificial después del INSERT de paciente: HTTP 500 y rollback
  de identidad, rol y paciente; reintento posterior correcto.
- Seeder repetido sin duplicar usuarios, roles ni asignaciones.
- Contraseña inicial inválida sin administrador parcial.
- Token vencido rechazado por HTTP sin esperas artificiales.

## Configuración y límites

InitialAdmin:Email e InitialAdmin:Password deben permanecer configurados
mediante secretos locales o variables de entorno.

El seeder no cambia la contraseña de un administrador existente.

El login de pacientes utiliza tpi:clinical-write y se serializa también
con las escrituras clínicas. Su efecto sobre rendimiento corresponde
a la aceptación integrada de C04.

La revocación es local y en memoria: se pierde al reiniciar la instancia
y no se distribuye entre procesos.

No se modificaron esquemas, migraciones ni snapshots.

Durante el trabajo se reportaron ocho advertencias de compilación.
Su detalle y clasificación local siguen pendientes de registrar.

La aprobación de estas pruebas no sustituye la aceptación global C04
ni declara terminado el backend.