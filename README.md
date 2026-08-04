# Sistema de Turnos Médicos

## Trabajo Práctico Integrador - Desarrollo de Software 2026

API REST desarrollada con ASP.NET Core para administrar especialidades, médicos, disponibilidades y citas médicas. El sistema utiliza autenticación JWT, autorización por roles, Entity Framework Core y SQL Server.

## Integrantes

- Fernández, Fausto Fidel
- López, Carlos Facundo
- Luna Jandar, Emilio
- Ríos Volentini, Federico

## Funcionalidades principales

- Autenticación de administradores mediante email y contraseña.
- Autenticación y alta automática de pacientes mediante email y DNI.
- Autorización diferenciada para los roles `Administrador` y `Paciente`.
- Gestión de especialidades y médicos con eliminación lógica.
- Configuración mensual de disponibilidades en intervalos de 30 minutos.
- Consulta de slots reservables por médico.
- Reserva y cancelación de citas con control de concurrencia.
- Consulta de citas activas del paciente.
- Búsqueda administrativa de citas por fecha, especialidad, médico y DNI.
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
- xUnit, Moq y EF Core InMemory

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

Antes de ejecutar la API, configurar una clave JWT segura y las credenciales del administrador inicial. La contraseña debe tener al menos ocho caracteres e incluir mayúscula, minúscula, número y carácter no alfanumérico.

Desde el **Explorador de soluciones** de Visual Studio:

1. Hacer clic derecho sobre el proyecto `Dsw2026Tpi.Api`.
2. Seleccionar **Administrar secretos de usuario**.
3. Reemplazar el contenido de `secrets.json` por una configuración local como la siguiente:

```json
{
  "Jwt": {
    "Key": "reemplazar-por-una-clave-segura-de-al-menos-32-caracteres",
    "Issuer": "Dsw2026Tpi.Api",
    "Audience": "Dsw2026Tpi.ApiUsers",
    "ExpiresInMinutes": 60
  },
  "InitialAdmin": {
    "Email": "admin@tpi.com",
    "Password": "Admin123!"
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
dotnet ef database update --context AuthenticationDbContext --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api

dotnet ef database update --context Dsw2026TpiDbContext --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api
```

## Ejecución

1. En el **Explorador de soluciones**, hacer clic derecho sobre `Dsw2026Tpi.Api` y seleccionar **Establecer como proyecto de inicio**.
2. Seleccionar el perfil HTTP de desarrollo en la barra superior.
3. Presionar `F5` para ejecutar con depuración o `Ctrl+F5` para ejecutar sin depuración.
4. Visual Studio compilará la solución, iniciará Kestrel y abrirá Swagger en el navegador.

En el perfil HTTP de desarrollo:

- API: `http://localhost:5278`
- Swagger: `http://localhost:5278/swagger`

Swagger solo se habilita en el ambiente `Development`.

Desde Swagger UI se pueden inspeccionar los contratos y probar los endpoints con **Try it out**. Visual Studio también permite ejecutar archivos `.http`, aunque Swagger es el recorrido principal documentado para este proyecto.

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
  "email": "admin@tpi.com",
  "password": "Admin123!"
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

Si la combinación de email y DNI todavía no existe, el paciente y su identidad se crean automáticamente. La respuesta de ambos logins tiene la siguiente estructura:

```json
{
  "token": "jwt-token",
  "role": "ADMINISTRADOR"
}
```

Para consumir endpoints protegidos, enviar el token en la cabecera:

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
| GET | `/api/doctors?pageSize={n}&pageIndex={n}&name={texto}` | Administrador o Paciente | Lista médicos activos con filtro y paginación |
| GET | `/api/doctors/{id}/availabilities` | Administrador | Obtiene la planificación mensual agrupada del médico |
| POST | `/api/doctors` | Administrador | Crea un médico |
| PUT | `/api/doctors/{id}` | Administrador | Actualiza un médico |
| DELETE | `/api/doctors/{id}` | Administrador | Elimina lógicamente un médico |

Ejemplo de alta o modificación:

```json
{
  "name": "Ana Pérez",
  "licenseNumber": "MP-12345",
  "specialityId": "00000000-0000-0000-0000-000000000000"
}
```

### Disponibilidades

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| GET | `/api/availabilities?doctorId={id}` | Paciente | Lista los slots futuros disponibles del médico |
| POST | `/api/availabilities` | Administrador | Crea la planificación mensual faltante |
| PUT | `/api/availabilities` | Administrador | Reemplaza los slots futuros no reservados del mes |

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

El sistema genera intervalos de 30 minutos, excluye horarios pasados y días no laborables, y evita solapamientos. La actualización conserva los slots reservados y los horarios que ya pasaron.

### Citas

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| POST | `/api/appointments` | Paciente | Reserva un slot disponible |
| GET | `/api/appointments/patient?dni={dni}` | Paciente | Lista las citas activas del paciente autenticado |
| DELETE | `/api/appointments/{id}` | Paciente | Cancela una cita propia y libera el slot |
| GET | `/api/appointments?date={yyyy-MM-dd}` | Administrador | Consulta las citas de una fecha |
| GET | `/api/appointments/search?specialtyId={id}&doctorId={id}&dni={dni}&date={yyyy-MM-dd}&pageSize={n}&pageIndex={n}` | Administrador | Realiza una búsqueda combinada y paginada |

Ejemplo de reserva:

```json
{
  "doctorId": "00000000-0000-0000-0000-000000000000",
  "availabilityId": "00000000-0000-0000-0000-000000000000",
  "patient": {
    "dni": 12345678
  },
  "reason": "Consulta de control"
}
```

La reserva valida que el paciente autenticado coincida con el DNI enviado, que el slot pertenezca al médico, sea futuro y se encuentre disponible. El control transaccional y de concurrencia evita reservas duplicadas.

### Estado de la API

| Método | Endpoint | Acceso | Descripción |
|---|---|---|---|
| GET | `/health-check` | Administrador o Paciente | Comprueba el estado general de la API |

## Códigos HTTP principales

- `200 OK`: consulta o autenticación exitosa.
- `201 Created`: recurso o cita creados.
- `204 No Content`: actualización o cancelación exitosa sin cuerpo de respuesta.
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

Visual Studio compila automáticamente antes de ejecutar. También puede compilarse manualmente desde **Compilar > Compilar solución**.

Para ejecutar las pruebas automatizadas:

1. Ir a **Prueba > Explorador de pruebas**.
2. Seleccionar **Ejecutar todas las pruebas**.
3. Verificar que el panel informe todas las pruebas aprobadas, sin fallos ni pruebas omitidas.

El proyecto cuenta con pruebas unitarias y de integración para servicios, persistencia, autenticación, autorización, revocación de tokens y rate limiting.

Los comandos equivalentes desde una terminal son:

```bash
dotnet build --nologo
dotnet test --nologo --verbosity minimal
```

Verificar problemas de formato antes de crear un commit:

```bash
git diff --check
```

## Estrategia de ramas

- `main`: versión estable del proyecto.
- `development`: rama de integración.
- `feature/*`: desarrollo de funcionalidades.
- `fix/*`: correcciones.
- `docs/*`: documentación.

Las ramas temporales se integran en `development` mediante Pull Requests y no deben eliminarse.
