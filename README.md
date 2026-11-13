# Sistema de Turnos Médicos

## Trabajo Práctico Integrador - Desarrollo de Software 2026

API REST desarrollada con ASP.NET Core para administrar especialidades, médicos, disponibilidades y citas médicas. El sistema utiliza autenticación JWT, autorización por roles, Entity Framework Core y SQL Server.

## Integrantes

- Ferreyra, Lucas Tomas
- Ferreyra, Matias Ignacio
- Lopez, Carlos Facundo
- Luna Jandar, Emilio

## Funcionalidades principales

- Autenticación de administradores mediante email y contraseña.
- Autenticación y alta automática de pacientes mediante email y DNI.
- Autorización diferenciada para los roles `Administrador` y `Paciente`.
- Gestión de especialidades y médicos con eliminación lógica.
- Configuración mensual de disponibilidades en intervalos de 30 minutos.
- Consulta de slots reservables por médico.
- Reserva y cancelación de citas con control de concurrencia.
- Consulta de citas activas del paciente.
- Búsqueda administrativa de citas por fecha, especialidad, médico, DNI y estado.
- Registro administrativo de citas atendidas o ausentes.
- Respuestas de error uniformes, logging y rate limiting.

## Tecnologías utilizadas

- Visual Studio
- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10.0.9
- SQL Server LocalDB
- ASP.NET Core Identity
- JWT Bearer
- Swagger / OpenAPI
- Serilog
- xUnit, Moq y pruebas de integración con EF Core InMemory y SQL Server LocalDB

## Estructura de la solución

- `Dsw2026Tpi.Api`: controladores, middleware, configuración y punto de entrada.
- `Dsw2026Tpi.Application`: servicios, DTO, interfaces y opciones de aplicación.
- `Dsw2026Tpi.Domain`: entidades e interfaces de persistencia.
- `Dsw2026Tpi.Data`: DbContext, repositorios, configuraciones y migraciones.
- `Dsw2026Tpi.CrossCutting`: roles, políticas, excepciones, recursos y modelos compartidos.
- `Dsw2026Tpi.Tests`: pruebas unitarias y de integración.

## Requisitos previos

- Visual Studio con la carga de trabajo **Desarrollo de ASP.NET y web**
- .NET SDK 10.0
- SQL Server LocalDB
- Git
- Componentes de Entity Framework Core incluidos en la solución y restaurados mediante NuGet

El uso de `dotnet-ef` 10.0.9 es opcional y solamente resulta necesario si se prefiere administrar las migraciones desde una terminal en lugar de la Consola del Administrador de paquetes de Visual Studio.

## Instalación

1. Abrir Visual Studio.
2. Seleccionar **Clonar un repositorio**.
3. Ingresar la URL `https://github.com/emiliolj1/dsw2026-tpi.git`.
4. Una vez clonado, seleccionar la rama `development` desde el selector de ramas de Git.
5. Abrir el archivo de solución `Dsw2026Tpi.slnx` ubicado en la raíz del repositorio.
6. Esperar a que Visual Studio restaure automáticamente los paquetes NuGet. Si fuera necesario, hacer clic derecho sobre la solución y seleccionar **Restaurar paquetes NuGet**.

La solución agrupa los proyectos de API, aplicación, dominio, persistencia, componentes transversales y pruebas, de acuerdo con el modelo en capas trabajado en la materia.

## Configuración

### Base de datos

La configuración de desarrollo utiliza SQL Server LocalDB mediante la cadena `ConnectionStrings:DefaultConnection` definida en:

```text
Dsw2026Tpi.Api/appsettings.Development.json
```

Para utilizar otra instancia de SQL Server, se puede reemplazar la configuración mediante la variable de entorno `ConnectionStrings__DefaultConnection`.

### JWT y administrador inicial

Antes de ejecutar la API, configurar una clave JWT segura de al menos 32 bytes UTF-8 y las credenciales del administrador inicial. La contraseña debe tener al menos ocho caracteres e incluir mayúscula, minúscula, número y carácter no alfanumérico.

Desde el **Explorador de soluciones** de Visual Studio:

1. Hacer clic derecho sobre el proyecto `Dsw2026Tpi.Api`.
2. Seleccionar **Administrar secretos de usuario**.
3. Agregar estos valores a `secrets.json`, reemplazando los marcadores por una clave JWT propia y la contraseña local del administrador:

```json
{
  "Jwt": {
    "Key": "REEMPLAZAR_CON_CLAVE_JWT_LOCAL",
    "Issuer": "Dsw2026Tpi.Api",
    "Audience": "Dsw2026Tpi.ApiUsers",
    "ExpiresInMinutes": 60
  },
  "InitialAdmin": {
    "Email": "admin@example.test",
    "Password": "REEMPLAZAR_CON_CLAVE_LOCAL"
  }
}
```

Los secretos de usuario se aplican solamente en el entorno `Development` y no se incorporan al repositorio. También pueden utilizarse variables de entorno con la notación de doble guion bajo, por ejemplo `Jwt__Key` e `InitialAdmin__Email`.

Al iniciar la aplicación, el seeder verifica de manera idempotente la existencia del administrador y de los roles requeridos.

No existe un endpoint público para registrar administradores. El administrador inicial se crea exclusivamente durante el inicio de la aplicación.

### Días no laborables

Los días en los que no deben generarse slots se configuran en `NonWorkingDays:Dates` con el formato `yyyy-MM-dd`:

```json
{
  "NonWorkingDays": {
    "Dates": [
      "2026-08-17",
      "2026-10-12"
    ]
  }
}
```

## Migraciones y creación de la base de datos

El proyecto utiliza dos contextos que comparten la misma base de datos. Para aplicar las migraciones desde Visual Studio:

1. Ir a **Herramientas > Administrador de paquetes NuGet > Consola del Administrador de paquetes**.
2. Ejecutar las migraciones de ambos contextos:

```powershell
Update-Database -Context AuthenticationDbContext -Project Dsw2026Tpi.Data -StartupProject Dsw2026Tpi.Api

Update-Database -Context Dsw2026TpiDbContext -Project Dsw2026Tpi.Data -StartupProject Dsw2026Tpi.Api
```

Como alternativa, si se trabaja desde una terminal con `dotnet-ef`, pueden utilizarse los comandos equivalentes:

```bash
sqllocaldb start MSSQLLocalDB
export ASPNETCORE_ENVIRONMENT=Development
dotnet ef database update --context AuthenticationDbContext --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api

dotnet ef database update --context Dsw2026TpiDbContext --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api
```

Para una base con datos previos, consultar la [guía de migraciones y preservación](docs/backend/migraciones.md) antes de aplicar cambios. La validación se realiza sobre una copia.

## Ejecución

1. En el **Explorador de soluciones**, hacer clic derecho sobre `Dsw2026Tpi.Api` y seleccionar **Establecer como proyecto de inicio**.
2. Seleccionar el perfil HTTP de desarrollo en la barra superior.
3. Presionar `F5` para ejecutar con depuración o `Ctrl+F5` para ejecutar sin depuración.
4. Visual Studio compilará la solución, iniciará Kestrel y abrirá Swagger en el navegador.

En el perfil HTTP de desarrollo:

- API: `http://localhost:5278`
- Swagger: `http://localhost:5278/swagger`

Swagger solo se habilita en el ambiente `Development`.

Desde Swagger UI se pueden inspeccionar los contratos y probar los endpoints con **Try it out**. Para ejecutar el recorrido de inicio de sesión, catálogo, agenda y reserva, abrir [Dsw2026Tpi.Api.http](Dsw2026Tpi.Api/Dsw2026Tpi.Api.http) en Visual Studio. Ejecutar una solicitud a la vez y completar las variables con los tokens e identificadores de las respuestas. Usar datos de desarrollo y conservar los marcadores de ejemplo en el archivo versionado.

## Autenticación y uso del token

Los únicos endpoints anónimos son:

- `POST /api/auth/admin/login`
- `POST /api/auth/patient/login`

Todos los demás endpoints requieren un JWT válido.

### Login de administrador

```http
POST /api/auth/admin/login
Content-Type: application/json
```

```json
{
  "email": "admin@example.test",
  "password": "REEMPLAZAR_CON_CLAVE_LOCAL"
}
```

### Login de paciente

```http
POST /api/auth/patient/login
Content-Type: application/json
```

```json
{
  "email": "paciente@tpi.com",
  "dni": 12345678
}
```

Si el email y el DNI todavía no están registrados, el paciente y su identidad se crean automáticamente. En los siguientes ingresos se debe usar la misma combinación. Ambos logins devuelven `token` y `role`. Por ejemplo, para el administrador:

```json
{
  "token": "jwt-token",
  "role": "ADMINISTRADOR"
}
```

Para el paciente, `role` es `PACIENTE`. Para consumir endpoints protegidos, enviar el token en la cabecera:

```http
Authorization: Bearer jwt-token
```

En Swagger, presionar `Authorize` e ingresar `Bearer` seguido del token.

## Endpoints implementados

### Autenticación

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| POST | `/api/auth/admin/login` | Público | Autentica al administrador inicial |
| POST | `/api/auth/patient/login` | Público | Autentica o registra automáticamente a un paciente |
| POST | `/api/auth/logout` | Administrador o Paciente | Revoca el token JWT utilizado en la solicitud |

### Especialidades

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/specialties?pageSize={n}&pageIndex={n}&name={texto}` | Administrador o Paciente | Lista especialidades activas con filtro y paginación |
| POST | `/api/specialties` | Administrador | Crea una especialidad |
| PUT | `/api/specialties/{id}` | Administrador | Actualiza una especialidad |
| DELETE | `/api/specialties/{id}` | Administrador | Elimina lógicamente una especialidad |

En los listados paginados, `pageSize` debe estar entre 1 y 100, `pageIndex` debe ser mayor o igual a 0 y el filtro `name` es opcional.

Ejemplo de alta o modificación:

```json
{
  "name": "Cardiología",
  "description": "Atención y diagnóstico cardiovascular"
}
```

### Médicos

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/doctors?pageSize={n}&pageIndex={n}&name={texto}&specialtyId={id}` | Administrador o Paciente | Lista médicos activos; `name` y `specialtyId` son filtros opcionales |
| GET | `/api/doctors/{id}/availabilities` | Administrador | Obtiene las reglas de planificación del médico para el mes vigente |
| POST | `/api/doctors` | Administrador | Crea un médico |
| PUT | `/api/doctors/{id}` | Administrador | Actualiza un médico |
| DELETE | `/api/doctors/{id}` | Administrador | Elimina lógicamente un médico |

Ejemplo de alta o modificación:

```json
{
  "name": "Ana Pérez",
  "licenseNumber": "MP-12345",
  "specialtyId": "00000000-0000-0000-0000-000000000000"
}
```

### Disponibilidades

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/availabilities?doctorId={id}` | Paciente | Lista los slots futuros disponibles del médico |
| POST | `/api/availabilities` | Administrador | Crea la planificación mensual faltante |
| PUT | `/api/availabilities` | Administrador | Reemplaza las reglas del mes vigente y recalcula los turnos libres futuros |

Los días pueden escribirse en español o inglés, sin distinción de mayúsculas, y los horarios deben utilizar el formato `HH:mm`:

```json
{
  "doctorId": "00000000-0000-0000-0000-000000000000",
  "days": [
    {
      "day": "Monday",
      "startTime": "09:00",
      "endTime": "12:00"
    },
    {
      "day": "Wednesday",
      "startTime": "14:00",
      "endTime": "17:00"
    }
  ]
}
```

Las respuestas de planificación normalizan `day` al español en mayúsculas
(por ejemplo, `MIÉRCOLES`). El sistema genera intervalos de 30 minutos, excluye
horarios pasados y días no laborables, y evita solapamientos. La actualización
conserva las reservas y el historial. Ver el [calendario operativo](docs/backend/calendario-operativo.md).

### Citas

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| POST | `/api/appointments` | Paciente | Reserva un slot disponible |
| GET | `/api/appointments/patient?dni={dni}` | Paciente | Lista las citas activas del paciente autenticado |
| DELETE | `/api/appointments/{id}` | Paciente | Cancela una cita propia y libera el slot |
| GET | `/api/appointments?date={yyyy-MM-dd}&status={estado}&pageSize={n}&pageIndex={n}` | Administrador | Consulta las citas de una fecha, con paginación y filtro opcional de estado |
| GET | `/api/appointments/search?specialtyId={id}&doctorId={id}&dni={dni}&date={yyyy-MM-dd}&status={estado}&pageSize={n}&pageIndex={n}` | Administrador | Realiza una búsqueda paginada con filtros opcionales combinables |
| PATCH | `/api/appointments/{id}/status` | Administrador | Marca como `ATTENDED` o `NO_SHOW` una cita `BOOKED` cuyo turno ya terminó |

Ejemplo de reserva:

```json
{
  "doctorId": "00000000-0000-0000-0000-000000000000",
  "availabilitySlotId": "00000000-0000-0000-0000-000000000000",
  "patient": {
    "dni": 12345678
  },
  "reason": "Consulta de control"
}
```

La reserva valida que el paciente autenticado coincida con el DNI enviado, que el slot pertenezca al médico, sea futuro y se encuentre disponible. El control transaccional y de concurrencia evita reservas duplicadas.

Para identificar la cita en la respuesta, usar `appointmentsId`. El estado se devuelve en `appointmentsStatus`. Los filtros de estado admiten `BOOKED`, `CANCELLED`, `ATTENDED` y `NO_SHOW`.

Ejemplo de actualización de estado:

```json
{
  "status": "ATTENDED"
}
```

### Estado de la API

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| GET | `/health-check` | Administrador o Paciente | Comprueba el estado general de la API |

## Códigos HTTP principales

- `200 OK`: consulta, autenticación o actualización exitosa. Las bajas de catálogo, la cancelación de citas y el logout devuelven `ok` como `text/plain`.
- `201 Created`: recurso, planificación o cita creados.
- `400 Bad Request`: datos inválidos o incumplimiento de una regla de negocio.
- `401 Unauthorized`: token ausente, inválido o credenciales incorrectas.
- `403 Forbidden`: el usuario no posee el rol requerido.
- `404 Not Found`: recurso inexistente.
- `409 Conflict`: conflicto de concurrencia o slot no disponible.
- `429 Too Many Requests`: límite de solicitudes excedido.
- `500 Internal Server Error`: error no controlado.

Las respuestas de error tienen un formato uniforme:

```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Uno o más errores de validación ocurrieron",
  "details": [
    {
      "field": "campo",
      "issue": "descripción del problema"
    }
  ]
}
```

La propiedad `details` se omite cuando el error no contiene información por campo.

## Rate limiting

La configuración predeterminada aplica ventanas fijas de 60 segundos:

- Login administrativo: 5 solicitudes por IP.
- Login de paciente: 10 solicitudes por IP.
- Reserva de citas: 5 solicitudes por usuario autenticado o IP.
- Límite general: 100 solicitudes por usuario autenticado o IP.

Los límites pueden modificarse en la sección `RateLimiting` de `appsettings.json`.

## Logging

Serilog registra eventos en consola y en archivos diarios dentro de la carpeta `Logs` del directorio de ejecución:

```text
Logs/
```

Los archivos rotan por día o al alcanzar 10 MB y se conservan hasta 30 archivos.

## Compilación y pruebas

Desde la raíz del repositorio:

```bash
dotnet restore
dotnet build --no-restore --nologo
dotnet test --no-build --filter "Category!=SqlServer" --nologo
```

Para la regresión SQL y la medición de rendimiento, iniciar LocalDB y configurar `DSW2026_TEST_SQL_CONNECTION` según la [guía de pruebas SQL](docs/backend/pruebas-sql.md). Ejecutar cada corrida por separado:

```bash
dotnet test --no-build --filter "Category=SqlServer&Purpose!=Performance" --nologo
dotnet test --no-build --filter "Purpose=Performance" --nologo
```

Las suites cubren servicios, persistencia, autenticación, autorización, concurrencia y el recorrido de aceptación. Para consultar los resultados y el alcance de cada validación, ver la [evidencia de aceptación](docs/backend/evidencia-aceptacion.md) y el [informe de rendimiento](docs/backend/rendimiento.md).

Verificar problemas de formato antes de crear un commit:

```bash
git diff --check
```

## Documentación del backend

- [Cierre del backend y evidencias](docs/backend/cierre-backend.md).
- [Guía de integración para frontend](docs/backend/integracion-frontend.md).
- [Contrato v1.7 de la API](docs/backend/contrato-v17.md).
- [Migraciones y preservación de datos](docs/backend/migraciones.md).
- [Pruebas de integración con SQL Server](docs/backend/pruebas-sql.md).
- [Seguimiento de la entrega](docs/backend/seguimiento.md).

## Estrategia de ramas

- `main`: versión estable del proyecto.
- `development`: rama de integración.
- `feature/*`: desarrollo de funcionalidades.
- `fix/*`: correcciones.
- `docs/*`: documentación.

Las ramas temporales se integran en `development` mediante Pull Requests y no deben eliminarse.
