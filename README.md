# Trabajo Práctico Integrador

## Desarrollo de Software 2026

API REST para la gestión de especialidades médicas, profesionales, disponibilidades y autenticación de usuarios.

## Integrantes

- Fernández, Fausto Fidel
- López, Carlos Facundo
- Luna Jandar, Emilio
- Ríos Volentini, Federico

## Tecnologías utilizadas

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core 10
- SQL Server LocalDB
- ASP.NET Core Identity
- JWT Bearer
- Swagger / OpenAPI
- xUnit
- Serilog

## Estructura del proyecto

- `Dsw2026Tpi.Api`: controladores, configuración y punto de entrada.
- `Dsw2026Tpi.Application`: servicios, DTOs e interfaces de aplicación.
- `Dsw2026Tpi.Domain`: entidades e interfaces del dominio.
- `Dsw2026Tpi.Data`: persistencia, configuraciones y migraciones.
- `Dsw2026Tpi.CrossCutting`: componentes compartidos.
- `Dsw2026Tpi.Tests`: pruebas automatizadas.

## Requisitos previos

- .NET SDK 10.0
- SQL Server LocalDB
- Git
- Herramienta `dotnet-ef` compatible con Entity Framework Core 10

Para comprobar la instalación:

```bash
dotnet --version
dotnet ef --version
```

Si `dotnet-ef` no está instalado:

```bash
dotnet tool install --global dotnet-ef --version 10.0.9
```

## Instalación

Clonar el repositorio:

```bash
git clone https://github.com/emiliolj1/dsw2026-tpi.git
cd dsw2026-tpi
```

Cambiar a la rama de desarrollo:

```bash
git switch development
```

Restaurar las dependencias:

```bash
dotnet restore
```

## Configuración de la base de datos

La conexión de desarrollo utiliza SQL Server LocalDB y se encuentra configurada en:

```text
Dsw2026Tpi.Api/appsettings.Development.json
```

Aplicar las migraciones del contexto de autenticación:

```bash
dotnet ef database update \
  --context AuthenticationDbContext \
  --project Dsw2026Tpi.Data \
  --startup-project Dsw2026Tpi.Api
```

Aplicar las migraciones del contexto principal:

```bash
dotnet ef database update \
  --context Dsw2026TpiDbContext \
  --project Dsw2026Tpi.Data \
  --startup-project Dsw2026Tpi.Api
```

## Administrador inicial

Antes de ejecutar la API se pueden configurar las credenciales del administrador inicial desde Git Bash:

```bash
export InitialAdmin__Email="admin@tpi.com"
export InitialAdmin__Password="Admin123!"
```

Las credenciales no deben incorporarse al repositorio.

Al iniciar la aplicación, el sistema verificará la existencia del administrador y del rol correspondiente.

## Ejecución

Ejecutar la API:

```bash
dotnet run --project Dsw2026Tpi.Api
```

Swagger estará disponible en:

```text
http://localhost:5278/swagger
```

El estado de la API puede verificarse mediante:

```text
GET /health-check
```

## Autenticación

### Registrar un administrador de prueba

```text
POST /auth/admin/register
```

Ejemplo:

```json
{
  "email": "admin@tpi.com",
  "password": "Admin123!"
}
```

### Iniciar sesión como administrador

```text
POST /auth/admin/login
```

Ejemplo:

```json
{
  "email": "admin@tpi.com",
  "password": "Admin123!"
}
```

La respuesta contiene un token JWT. Para utilizar los endpoints protegidos, debe ingresarse en Swagger mediante el botón `Authorize`:

```text
Bearer TOKEN_GENERADO
```

### Iniciar sesión como paciente

```text
POST /auth/patient/login
```

Ejemplo:

```json
{
  "email": "paciente@tpi.com",
  "dni": 12345678
}
```

## Endpoints disponibles

### Autenticación

| Método | Endpoint | Descripción |
|---|---|---|
| POST | `/auth/admin/register` | Registra un administrador de prueba |
| POST | `/auth/admin/login` | Autentica un administrador |
| POST | `/auth/patient/login` | Autentica un paciente |

### Especialidades

Requieren autorización con rol `Administrador`.

| Método | Endpoint | Descripción |
|---|---|---|
| GET | `/specialties` | Lista especialidades con filtros y paginación |
| POST | `/specialties` | Crea una especialidad |
| PUT | `/specialties/{id}` | Actualiza una especialidad |
| DELETE | `/specialties/{id}` | Elimina lógicamente una especialidad |

### Médicos

Requieren autorización con rol `Administrador`.

| Método | Endpoint | Descripción |
|---|---|---|
| GET | `/doctors` | Lista médicos con filtros y paginación |
| POST | `/doctors` | Crea un médico |
| PUT | `/doctors/{id}` | Actualiza un médico |
| DELETE | `/doctors/{id}` | Elimina lógicamente un médico |
| GET | `/doctors/{id}/availabilities` | Obtiene la planificación del médico |

### Disponibilidades

Requieren autorización con rol `Administrador`.

| Método | Endpoint | Descripción |
|---|---|---|
| POST | `/availabilities` | Crea la planificación de disponibilidad |
| PUT | `/availabilities` | Actualiza la planificación de disponibilidad |

## Compilación y pruebas

Compilar la solución:

```bash
dotnet build
```

Ejecutar las pruebas automatizadas:

```bash
dotnet test
```

Verificar problemas de formato:

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