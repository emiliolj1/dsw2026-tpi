# Seguimiento del cierre del backend

## Base de trabajo

- Fecha de verificación inicial: 2026-10-07.
- Responsable de la verificación inicial: Emilio.
- Rama de integración: development.
- Commit base inicial: 580eddcb9d0b1e18698a0d02bebed8d64b3dfd84.
- Rama inicial de documentación: docs/emilio-contratos-cierre.
- Entorno inicial: Windows y Git Bash.
- SDK .NET inicial: 10.0.302.
- Runtime informado por el ejecutor inicial de pruebas: .NET 10.0.10.
- Paquetes EF Core consultados en Data y Tests: 10.0.9.
- Herramienta CLI dotnet-ef inicial: 10.0.9.

Última integración verificada por Charly:
- Fecha: 2026-10-15.
- Commit de development: 1645370.
- G01 y G02 integrados mediante PR #21, SHA de merge 11cd043.
- C01 integrado mediante PR #22, SHA de merge 1645370.

## Evidencia inicial

Resultados ejecutados por Emilio sobre el commit base, antes de modificar código:

| Comando | Resultado |
|---|---|
| `dotnet restore` | Correcto |
| `dotnet build --no-restore --nologo` | Correcto |
| `dotnet test --no-build --nologo --verbosity minimal` | 54 aprobadas, 0 fallidas, 0 omitidas |

La suite inicial aprobada no acredita por sí sola el cumplimiento completo
de la consigna ni la atomicidad y concurrencia en SQL Server real.

En esta verificación inicial, la infraestructura SQL de C01 estaba pendiente.
C01 se integró posteriormente mediante el PR #22; su evidencia se detalla
en docs/backend/pruebas-sql.md.

## Estado de tareas

Una tarea solo se considera integrada cuando su PR fue fusionado en
development y se registraron el SHA y las evidencias correspondientes.
Un PR abierto no habilita las dependencias.

Las ramas de tareas pendientes son propuestas.
El símbolo "-" indica información todavía no disponible.
Los SHA de la columna "SHA integrado" identifican el commit de merge
en development.

| Tarea | Responsable | Dependencias | Estado | Rama | PR | SHA integrado | Evidencia |
|---|---|---|---|---|---|---|---|
| G01 | Emilio | Ninguna | Integrada | docs/emilio-contratos-cierre | #21 | 11cd043 | Contrato y planificación integrados junto con G02 |
| G02 | Emilio | G01, incluido en el mismo PR | Integrada | docs/emilio-contratos-cierre | #21 | 11cd043 | 72 pruebas originales aprobadas; alcance SQL verificado posteriormente en C01 |
| C01 | Charly | G02 | Integrada | test/charly-sqlserver-base | #22 | 1645370 | 77 pruebas no SQL y 5 SQL aprobadas, sin fallidas ni omitidas; docs/backend/pruebas-sql.md |
| C02 | Charly | G02 | Pendiente | - | - | - | - |
| N01 | Nacho | G02 | Pendiente | - | - | - | - |
| L01 | Lucas | G02 | Pendiente | - | - | - | - |
| E01 | Emilio | G02, C01 | Pendiente | fix/emilio-autenticacion-cierre | - | - | - |
| C03 | Charly | C01, C02 | Pendiente | - | - | - | - |
| N02 | Nacho | N01 | Pendiente | - | - | - | - |
| L02 | Lucas | L01, C01, N01 | Pendiente | - | - | - | - |
| E02 | Emilio | G02, C01, E01 | Pendiente | fix/emilio-api-transversal | - | - | - |
| N03 | Nacho | N02, C01, L02 | Pendiente | - | - | - | - |
| L03 | Lucas | L02 | Pendiente | - | - | - | - |
| N04 | Nacho | N03, E01, L03 | Pendiente | - | - | - | - |
| L04 | Lucas | L03, N03, E01 | Pendiente | - | - | - | - |
| C04 | Charly | E01, E02, C03, N04, L04 | Pendiente | - | - | - | - |
| C05 | Charly | C04, N04 | Pendiente | - | - | - | - |
| E03 | Emilio | N04, L04, C05, E01, E02 | Pendiente | docs/emilio-cierre-backend | - | - | - |

## Entrega conjunta G01 + G02

Por decisión de Emilio del 2026-10-07, ambas tareas se entregaron
en un único PR desde docs/emilio-contratos-cierre hacia development.

Integración:
- PR: #21.
- SHA de merge: 11cd043.
- Commit de implementación de G02: 723e782.

G02 incorpora:
- Reloj configurable con TimeProvider y validación al iniciar.
- Interfaces y fábrica del alcance transaccional SQL.
- Configuración y registro Scoped de la fábrica.
- Excepción transversal de conflicto de escritura.
- Paginación con desempate por Id y validación de desbordamientos.

Evidencia local reportada por Emilio después de los cambios:
- Compilación correcta.
- Suite completa de ese momento: 72 aprobadas, 0 fallidas, 0 omitidas.
- ClinicClockTests: 10 aprobadas.
- PersistenceEfTests: 10 aprobadas.
- git diff --check: sin problemas reportados.

Estado actualizado al 15/10/2026:
- PR conjunto #21 integrado en development mediante 11cd043.
- Bloqueo, timeout y rollback verificados con SQL Server real en C01,
  integrado mediante PR #22 y SHA 1645370.

Pendientes:
- Adopción del reloj y alcance por los módulos en sus tareas asignadas.

La publicación de la fábrica no implica que las operaciones existentes
ya estén protegidas por el bloqueo compartido.

## Entrega C01 — Infraestructura SQL Server

Responsable: Charly.

Integración:
- Rama: test/charly-sqlserver-base.
- PR: #22.
- Commit de implementación: ba37a50.
- SHA de merge en development: 1645370.
- Fecha de verificación local: 2026-10-15.

Entregables:
- SqlServerTestConnection: configuración exclusiva y nombres únicos.
- SqlServerTestConnectionTests: validaciones sin conexión SQL.
- SqlServerWebApplicationFactory: host SQL con configuración aislada.
- SqlServerFixture: migraciones, arranque y limpieza de la base de pruebas.
- SqlServerInfrastructureTests: migraciones, seeder real y arranque HTTP.
- ClinicalWriteScopeSqlTests: commit, rollback y conflicto entre conexiones.
- docs/backend/pruebas-sql.md: configuración, ejecución y reutilización.

Entorno utilizado:
- Windows y Git Bash.
- SDK .NET 10.0.400-preview.0.26322.102.
- Runtime .NET 10.0.9.
- Instancia LocalDB: MSSQLLocalDB.
- Versión LocalDB: 17.0.4025.3.

Resultados ejecutados por Charly:

| Comando | Resultado |
|---|---|
| `dotnet restore` | Correcto, con advertencias de paquetes |
| `dotnet build --no-restore --nologo` | Correcto, sin errores y con 8 advertencias |
| `dotnet test --no-build --filter "Category!=SqlServer" --nologo` | 77 aprobadas, 0 fallidas, 0 omitidas |
| `dotnet test --no-build --filter "Category=SqlServer" --nologo --logger trx` | 5 aprobadas, 0 fallidas, 0 omitidas |
| `git diff --cached --check` | Sin observaciones |

Las cinco pruebas de SqlServerTestConnectionTests están incluidas
en las 77 pruebas no SQL; no deben sumarse nuevamente.

Verificaciones SQL:
- Migraciones aplicadas en ambos contextos.
- Sin migraciones pendientes en la base creada.
- Administrador inicial creado mediante IdentitySeeder real.
- Contraseña y rol del administrador verificados.
- API iniciada y health-check protegido con HTTP 401.
- Escritura confirmada mediante CompleteAsync.
- INSERT revertido al disponer el alcance sin CompleteAsync.
- Conflicto por timeout desde una segunda conexión independiente.
- Reintento exitoso después de liberar el primer bloqueo.

Configuración:
- Variable exclusiva: DSW2026_TEST_SQL_CONNECTION.
- Catálogos generados: Dsw2026Tpi_Test_<GUID>.
- Credenciales ficticias y JWT de pruebas definidos por host.
- No se modificó el proveedor de producción.
- No se agregaron ni modificaron migraciones.
- Resultados TRX locales excluidos por .gitignore.

Límites:
- La prueba de timeout verifica ClinicalWriteConflictException.
  Su traducción a HTTP 409 se verificará en la integración correspondiente.
- Las carreras de reserva, agenda y bajas siguen asignadas a las tareas
  de los módulos y a la aceptación integrada.
- C01 no constituye una medición de rendimiento.
- Las migraciones futuras de N04 deberán verificarse al integrarse.
- La ejecución conjunta de pruebas SQL y no SQL en un único proceso
  no fue acreditada; se verificaron mediante comandos separados.

## Pendientes transversales conocidos

- Advertencias NU1902 y NU1903 relacionadas con
  System.Security.Cryptography.Pkcs 9.0.4, reportadas en la ejecución
  de Charly. Coordinar su resolución con Emilio durante el cierre
  de dependencias.
- Mantener actualizados PR, SHA y evidencias al integrar cada tarea.
- No presentar pruebas omitidas o no ejecutadas como aprobadas.
- Conservar los datos existentes y utilizar bases exclusivas de pruebas.
