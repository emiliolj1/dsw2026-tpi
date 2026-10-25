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

Última verificación de integraciones: 2026-10-25.
- Commit remoto de development: a9d3930ef23846c1b9f761165e116f65b8f41551.
- G01 y G02: PR #21, SHA de merge 11cd043.
- C01: PR #22, SHA de merge 1645370.
- C02: PR #24, SHA de merge 11cc6fe.
- C03: PR #25, SHA de merge a39cfab.
- L01: código integrado por PR #26, SHA de merge 82e2f0e;
  evidencia de ejecución específica pendiente de registrar.
- E01: PR #27, SHA de merge a9d3930.
- E02: verificada localmente en fix/emilio-api-transversal;
  todavía sin PR ni SHA integrado.

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
| C02 | Charly | G02 | Integrada | feature/charly-catalogos-v17 | #24 | 11cc6fe | 87 pruebas no SQL y 7 SQL aprobadas; evidencia en la sección C02 |
| N01 | Nacho | G02 | Pendiente | - | - | - | - |
| L01 | Lucas | G02 | Código integrado; evidencia de ejecución pendiente | feature/lucas-contratos-citas | #26 | 82e2f0e | PR fusionado; no se atribuyen resultados no registrados |
| E01 | Emilio | G02, C01 | Integrada | fix/emilio-autenticacion-cierre | #27 | a9d3930 | 112 pruebas no SQL y 46 SQL aprobadas; docs/backend/evidencia-autenticacion.md |
| C03 | Charly | C01, C02 | Integrada | fix/charly-bajas-relacionadas | #25 | a39cfab | 93 pruebas no SQL y 26 SQL aprobadas; concurrencia con reservas pendiente para C04 |
| N02 | Nacho | N01 | Pendiente | - | - | - | - |
| L02 | Lucas | L01, C01, N01 | Pendiente | - | - | - | - |
| E02 | Emilio | G02, C01, E01 | Implementada y verificada localmente; pendiente de PR | fix/emilio-api-transversal | - | - | 116 pruebas no SQL y 62 SQL aprobadas; navegador y dependencias verificados; evidencia-api-transversal.md |
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

## C02 — Contratos y filtros de catálogos

Responsable: Charly.
Fecha de verificación local: 2026-10-15.
Rama: feature/charly-catalogos-v17.
Base de trabajo: 2e27c5b.
Estado: integrada mediante PR #24, aprobado por Emilio.
SHA de merge en development: 11cc6fe.

Cambios:
- POST/PUT de médicos recibe specialtyId mediante serialización explícita,
  conservando SpecialityId como nombre interno C#.
- GET de médicos admite specialtyId opcional, combinado con name.
- El filtro excluye médicos inactivos o eliminados y especialidades eliminadas.
- Los filtros se aplican antes del conteo y la paginación.
- Ambos catálogos usan pageSize=10 y pageIndex=0 por defecto.
- Se conservan las validaciones de tamaño 1–100 y nombre de 3–100 caracteres.
- Ambos DELETE responden HTTP 200, text/plain y cuerpo exacto ok.
- Se actualizó la declaración de respuesta DELETE en Swagger.
- Se conserva la respuesta de médico con specialty anidada.

Evidencia:
- DoctorServiceTests: 13 aprobadas.
- DoctorServiceTests y SpecialityServiceTests: 22 aprobadas.
- CatalogContractTests: 7 aprobadas.
- CatalogFilterSqlTests: 2 aprobadas con SQL Server real.
- Regresión completa no SQL: 87 aprobadas, 0 fallidas, 0 omitidas.
- Regresión completa SQL: 7 aprobadas, 0 fallidas, 0 omitidas.
- git diff --check sin observaciones.

Los resultados específicos están incluidos en las suites completas;
no deben sumarse nuevamente.

Se verificaron JSON literal, POST/PUT con entidad de respuesta,
DELETE text/plain, valores predeterminados, permisos existentes,
filtros combinados y páginas sin duplicados con nombres repetidos.

La ejecución SQL sin la variable de conexión falló con un diagnóstico
explícito. Después de configurarla, los casos pasaron.

Para consumir los catálogos:
- Usar specialtyId como nombre público.
- Omitir name cuando se limpia una búsqueda.
- Leer DELETE como texto, sin intentar convertir ok a JSON.

Sin cambios de esquema, migraciones ni acción GetMonthlyPlanning.
Las políticas de bajas relacionadas y su atomicidad corresponden a C03.

## C03 — Bajas y relaciones de catálogos

Responsable: Charly.
Fecha de verificación local: 2026-10-16.
Rama: fix/charly-bajas-relacionadas.
Base de trabajo: 11cc6fe.
Estado: integrada mediante PR #25.
SHA de merge en development: a39cfab4f69c38eed45e2b254f2cf1be44d67c76.

Cambios:
- Altas, modificaciones y bajas de médicos y especialidades utilizan
  IClinicalWriteScopeFactory.
- Las lecturas decisorias se realizan dentro del alcance de escritura.
- Una especialidad con médicos activos y no eliminados devuelve
  HTTP 409 con SPECIALTY_IN_USE.
- La baja o reasignación de un médico con citas BOOKED cuyo inicio
  no haya pasado devuelve HTTP 409 con DOCTOR_HAS_FUTURE_APPOINTMENTS.
- La comparación utiliza el reloj local del centro.
- El instante exacto de inicio también impide la operación.
- Se permite editar nombre o matrícula conservando la especialidad.
- Las bajas son lógicas y conservan el historial consultable.
- Los controladores documentan las respuestas 409 en Swagger.
- La fábrica HTTP InMemory utiliza un sustituto del alcance,
  exclusivo de pruebas de contrato. Las garantías SQL se verifican
  mediante la fábrica SQL real.

Resultados:
- DoctorServiceTests: 16 aprobadas.
- SpecialityServiceTests: 12 aprobadas.
- CatalogRelationsSqlTests: 19 casos incluidos en la regresión SQL.
- Regresión completa no SQL: 93 aprobadas, 0 fallidas, 0 omitidas.
- Regresión completa SQL: 26 aprobadas, 0 fallidas, 0 omitidas.
- git diff --check sin observaciones.

Las suites completas incluyen los casos específicos;
los resultados no deben sumarse nuevamente.

Verificaciones:
- Especialidades en uso y relaciones con médicos inactivos o eliminados.
- Rechazo de baja y reasignación con reservas futuras.
- Límites de fecha y hora con reloj fijo.
- Tratamiento de citas canceladas, atendidas, ausentes y eliminadas.
- Conservación de datos y fechas de auditoría en los rechazos comprobados.
- Consulta histórica después de eliminar médico y especialidad.
- Rechazo de disponibilidad por HTTP para un médico eliminado,
  utilizando login real de paciente.
- Rechazo de una nueva reserva mediante el servicio real,
  sin crear citas ni modificar el slot libre.
- Reasignación permitida sin reservas futuras y posterior baja
  de la especialidad anterior.

Pendiente de integración cruzada:
- Carrera entre baja de médico y reserva simultánea.
- Ejecutarla en C04 cuando L02 utilice el alcance común.
- C03 no demuestra todavía la serialización conjunta con el módulo
  de reservas pendiente de Lucas.

Sin nuevas migraciones ni modificaciones de los módulos de agenda
y citas. No se cambió GetMonthlyPlanning.

## L01 — Contratos de citas

- Responsable: Lucas.
- Rama: feature/lucas-contratos-citas.
- PR integrado: #26.
- SHA de merge: 82e2f0e40f584b9b035cab625cc4be6901fd949e.
- La integración del código fue comprobada en GitHub.
- Falta registrar la evidencia de ejecución específica de Lucas.
- No se presenta esa evidencia pendiente como una prueba aprobada.
- L02 mantiene su dependencia de N01, todavía pendiente.

## E01 — Cierre de autenticación

- Responsable: Emilio.
- PR integrado: #27.
- SHA de merge: a9d3930ef23846c1b9f761165e116f65b8f41551.
- Evidencia histórica: 112 pruebas no SQL y 46 SQL aprobadas.
- Detalle: [evidencia-autenticacion.md](evidencia-autenticacion.md).

## E02 — API transversal

- Responsable: Emilio.
- Fecha de verificación local: 2026-10-25.
- Rama: fix/emilio-api-transversal.
- Base: a9d3930ef23846c1b9f761165e116f65b8f41551.
- Estado: implementación local verificada; pendiente de PR e integración.
- Regresión no SQL: 116 aprobadas, 0 fallidas, 0 omitidas.
- Regresión SQL: 62 aprobadas, 0 fallidas, 0 omitidas.
- Compilación final sin advertencias reportadas.
- CORS verificado por HTTP y en navegador, con JWT real.
- Swagger accesible y migraciones existentes aplicadas en PC02.
- Data y Tests no reportaron vulnerabilidades conocidas al consultar
  dependencias directas y transitivas el 2026-10-25.
- Detalle: [evidencia-api-transversal.md](evidencia-api-transversal.md).
- Calendario acordado: [calendario-operativo.md](calendario-operativo.md).
- C04 sigue dependiendo de la integración de E02, N04 y L04,
  además de E01 y C03 ya integradas.

## Pendientes transversales conocidos

- Las advertencias NU1902/NU1903 de Pkcs 9.0.4 se resolvieron
  localmente en E02 fijando Pkcs 9.0.21. La corrección aún debe
  integrarse mediante el PR de E02.
- Confirmar cierres adicionales del centro y su aplicación a la agenda
  en N02 y C04; actualizar el calendario antes de operar en 2027.
- Registrar la evidencia específica pendiente de L01.
- Mantener actualizados PR, SHA y evidencias al integrar cada tarea.
- No presentar pruebas omitidas o no ejecutadas como aprobadas.
- Conservar los datos existentes y utilizar bases exclusivas de pruebas.
