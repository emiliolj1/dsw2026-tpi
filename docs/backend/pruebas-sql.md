# Pruebas de integración con SQL Server — C01

## Objetivo

Verificar el comportamiento relacional utilizando SQL Server real:
migraciones, administrador inicial, confirmación y reversión de
escrituras, exclusión entre conexiones y timeout del bloqueo clínico.

Las pruebas InMemory existentes se conservan, pero no constituyen
evidencia de transacciones ni bloqueos SQL.

## Requisitos

- SDK compatible con net10.0.
- SQL Server o LocalDB disponible.
- Permisos para crear y eliminar bases exclusivas de pruebas.
- Ejecutar los comandos desde la raíz del repositorio.

Entorno verificado el 15/10/2026:
- Windows, equipo EQUIPOCARLOS2.
- SDK 10.0.400-preview.0.26322.102.
- Runtime .NET 10.0.9.
- LocalDB MSSQLLocalDB, versión 17.0.4025.3.
- Rama de trabajo: test/charly-sqlserver-base.
- Base de development: e810008.
- G02 integrado mediante PR #21, commit de implementación 723e782.

## Configuración en Git Bash

Iniciar LocalDB:

```bash
sqllocaldb start MSSQLLocalDB
```

Configurar la conexión exclusiva en la terminal que ejecutará las pruebas:

```bash
export DSW2026_TEST_SQL_CONNECTION='Server=(localdb)\MSSQLLocalDB;Database=Dsw2026Tpi_Test_Config;Integrated Security=true;TrustServerCertificate=true;'
```

La variable pertenece a esa terminal y a sus procesos hijos.
Debe configurarse nuevamente al abrir otra terminal.

Para otro servidor SQL, adaptar Server y autenticación, conservando
un catálogo de configuración con el prefijo Dsw2026Tpi_Test_.

## Aislamiento y limpieza

SqlServerTestConnection valida la configuración y genera un catálogo
Dsw2026Tpi_Test_<GUID> por instancia del fixture.

Dsw2026Tpi_Test_Config no es la base que se crea: funciona como
catálogo de configuración para validar el uso exclusivo de pruebas.

Se rechazan:
- Catálogos sin el prefijo requerido.
- Conexiones sin servidor.
- Archivos adjuntos mediante AttachDBFilename.

SqlServerFixture aplica primero las migraciones de Identity y luego
las del dominio. Después inicia la API, que ejecuta IdentitySeeder real.

La limpieza elimina exclusivamente el catálogo generado, validando
su coincidencia con la conexión y el sufijo GUID.

No usar conexiones de producción ni bases compartidas. Un cierre
forzado del proceso puede impedir la limpieza automática; cualquier
base residual debe identificarse antes de eliminarla manualmente.

## Componentes reutilizables

Namespace: Dsw2026Tpi.Tests.Integration.

- SqlServerTestConnection: valida y prepara la conexión exclusiva.
- SqlServerWebApplicationFactory: API con proveedor SQL Server,
  configuración aislada por host y administrador ficticio de pruebas.
- SqlServerFixture: migraciones, arranque y limpieza.

Ejemplo para nuevos tests:

```csharp
[Trait("Category", "SqlServer")]
public sealed class ExampleSqlTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public ExampleSqlTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }
}
```

El fixture expone:
- Client: cliente HTTP de la API.
- Factory: acceso a servicios y creación de scopes.
- Connection: conexión y nombre del catálogo generado.
- CreateApplicationContext(): contexto nuevo del dominio.
- CreateAuthenticationContext(): contexto nuevo de Identity.

Para concurrencia, usar contextos y conexiones independientes
del mismo fixture. No compartir un DbContext entre operaciones paralelas.

Cada clase con IClassFixture recibe su propia base. Los tests de una
misma clase comparten el fixture: usar IDs únicos y no depender
del orden de ejecución.

## Ejecución

```bash
dotnet restore
dotnet build --no-restore --nologo
dotnet test --no-build --filter "Category!=SqlServer" --nologo
dotnet test --no-build --filter "Category=SqlServer" --nologo --logger trx
```

Ejecutar ambas categorías por separado, como se verificó en C01.
La fábrica InMemory preexistente utiliza variables globales de entorno;
C01 no modifica ese componente.

Si falta DSW2026_TEST_SQL_CONNECTION, el fixture lanza un error explícito.
Los problemas de conexión, migración o arranque también hacen fallar
las pruebas; no se convierten en pruebas omitidas.

Los archivos TRX quedan en Dsw2026Tpi.Tests/TestResults y están
excluidos por .gitignore.

## Evidencia de ejecución — 15/10/2026

| Suite | Total | Correctas | Fallidas | Omitidas |
|---|---:|---:|---:|---:|
| Category!=SqlServer | 77 | 77 | 0 | 0 |
| Category=SqlServer | 5 | 5 | 0 | 0 |

Casos SQL verificados:
- Migraciones aplicadas en ambos contextos, sin migraciones pendientes.
- Administrador creado por el seeder real, contraseña válida y rol asignado.
- API iniciada; health-check devuelve 401 sin autenticación.
- CompleteAsync confirma una escritura consultable desde otro contexto.
- Dispose sin CompleteAsync revierte un INSERT enviado a SQL.
- Segundo escritor recibe ClinicalWriteConflictException por timeout
  mientras otro conserva el bloqueo, y puede reintentar al liberarse.

También pasaron cinco casos sin conexión SQL que verifican el rechazo
de catálogos ajenos, nombres únicos y rechazo de archivos adjuntos.

La compilación informó ocho advertencias NU1902/NU1903 relacionadas
con System.Security.Cryptography.Pkcs 9.0.4. Pendiente de coordinar
con Emilio durante el cierre de dependencias.

## Límites y coordinación

- C01 verifica la excepción de conflicto del alcance SQL.
  Su traducción a HTTP 409 se verificará en la integración correspondiente.
- No demuestra todavía carreras de reserva, agenda o bajas.
- No constituye una medición de rendimiento.
- No agrega ni modifica migraciones.
- Incorporar y verificar las migraciones de N04 cuando se integren.
- Nacho y Lucas pueden reutilizar el fixture y los contextos independientes.
- Registrar PR y SHA integrado al completar la revisión y el merge.
