# Contrato común del backend — DSW2026 TPI v1.7

## 1. Propósito y estado

Contrato objetivo para coordinar el cierre del backend y su posterior
integración con React.

Fuentes:
- Consigna DSW2026 TPI v1.7.
- Guía general de cierre del backend para cuatro integrantes.
- Guía individual de Emilio.

Base inicial: 580eddcb9d0b1e18698a0d02bebed8d64b3dfd84.
Rama de integración: development.

Este documento describe el comportamiento que debe alcanzarse.
La implementación y sus pruebas se registran en seguimiento.md.
Publicar este contrato no equivale a completar las tareas de código.

## 2. Convenciones generales

- URL local prevista de la API: http://localhost:5278.
- Origen local previsto del frontend: http://localhost:5173.
- Identificadores de entidades: GUID.
- JSON público en camelCase.
- Fechas operativas: YYYY-MM-DD.
- Horarios: HH:mm.
- Las reglas operativas usan la fecha y hora del centro en Argentina.
- La auditoría técnica utiliza UTC.
- Autenticación mediante Authorization: Bearer <token>.
- Roles públicos: ADMINISTRADOR y PACIENTE.
- Solo los dos endpoints de login son anónimos.
- Las bajas son lógicas y deben preservar las relaciones históricas.
- Las respuestas JSON utilizan application/json.
- DELETE de especialidad, médico y cita devuelve HTTP 200,
  Content-Type text/plain y cuerpo literal ok, sin comillas JSON.
- Las colecciones sin resultados devuelven arrays vacíos, no null.

Los nombres C# internos pueden conservarse usando mapeos o atributos
de serialización. No se exige renombrar columnas para corregir el JSON.

## 3. Paginación

Se aplica a:
- GET /api/specialties.
- GET /api/doctors.
- GET /api/appointments.
- GET /api/appointments/search.

Parámetros:
- pageSize: valor predeterminado 10; rango permitido 1 a 100.
- pageIndex: valor predeterminado 0; entero no negativo.
- Un desplazamiento fuera del rango admitido produce HTTP 400.

Formato:

```json
{
  "pageSize": 10,
  "pageIndex": 0,
  "data": [],
  "total": 0
}
```

total representa la cantidad de registros que cumplen los filtros antes
de paginar. Una página sin elementos puede conservar un total mayor que cero.

Orden:
- Catálogos: nombre y luego Id como desempate.
- Citas: fecha y hora de inicio, con Id como desempate.

El orden debe ser estable para un mismo conjunto de datos.

## 4. Endpoints originales de la consigna

En esta tabla, Admin significa ADMINISTRADOR y Paciente significa PACIENTE.
Las respuestas descritas son las de éxito.

| Método | Ruta | Acceso | Respuesta |
|---|---|---|---|
| POST | /api/auth/admin/login | Anónimo | 200: token y role |
| POST | /api/auth/patient/login | Anónimo | 200: token y role |
| GET | /api/specialties | Admin/Paciente | 200: página de especialidades |
| POST | /api/specialties | Admin | 201: especialidad creada |
| PUT | /api/specialties/{id} | Admin | 200: especialidad actualizada |
| DELETE | /api/specialties/{id} | Admin | 200 text/plain: ok |
| GET | /api/doctors | Admin/Paciente | 200: página de médicos |
| GET | /api/doctors/{id}/availabilities | Admin | 200: array de reglas del mes actual |
| POST | /api/doctors | Admin | 201: médico creado |
| PUT | /api/doctors/{id} | Admin | 200: médico actualizado |
| DELETE | /api/doctors/{id} | Admin | 200 text/plain: ok |
| POST | /api/availabilities | Admin | 201: planificación resultante |
| PUT | /api/availabilities | Admin | 200: planificación resultante |
| POST | /api/appointments | Paciente | 201: DTO común de cita |
| GET | /api/appointments/patient | Paciente | 200: array de citas propias activas |
| DELETE | /api/appointments/{id} | Paciente | 200 text/plain: ok |
| GET | /api/appointments | Admin | 200: página de citas de una fecha |
| GET | /api/appointments/search | Admin | 200: página de citas filtradas |

## 5. Autenticación

Login administrativo:

```json
{
  "email": "admin@example.com",
  "password": "Ejemplo123!"
}
```

- Email obligatorio y con formato válido.
- Contraseña obligatoria, con un mínimo de 8 caracteres.
- Las credenciales reales se configuran fuera del repositorio.
- El administrador inicial y los roles se crean de forma idempotente.

Login de paciente:

```json
{
  "email": "paciente@example.com",
  "dni": 30123456
}
```

- Email obligatorio y válido.
- DNI de 7 u 8 dígitos.
- El primer acceso válido registra al paciente.
- Los reingresos no deben duplicar la identidad ni el paciente.
- Las combinaciones incompatibles de email e identidad deben rechazarse.

Respuesta:

```json
{
  "token": "jwt-token",
  "role": "PACIENTE"
}
```

El login administrativo devuelve role ADMINISTRADOR.

## 6. Especialidades

POST y PUT reciben:

```json
{
  "name": "Cardiología",
  "description": "Atención y diagnóstico cardiovascular"
}
```

Validaciones:
- name obligatorio, entre 3 y 100 caracteres.
- description obligatorio, entre 10 y 100 caracteres.
- En PUT debe existir la especialidad activa.

GET admite pageSize, pageIndex y name.
Si name se proporciona, debe tener entre 3 y 100 caracteres.
Sin name, no se aplica ese filtro.
Los listados excluyen especialidades eliminadas.

Entidad de respuesta para alta, actualización y elementos del listado:

```json
{
  "id": "44444444-4444-4444-8444-444444444444",
  "name": "Cardiología",
  "description": "Atención y diagnóstico cardiovascular"
}
```

Política acordada de baja:
- No eliminar una especialidad con médicos activos.
- El conflicto produce HTTP 409.
- La baja debe conservar la historia consultable.

## 7. Médicos

POST y PUT reciben:

```json
{
  "name": "Ana Pérez",
  "licenseNumber": "MP-12345",
  "specialtyId": "44444444-4444-4444-8444-444444444444"
}
```

Validaciones:
- name obligatorio, entre 3 y 100 caracteres.
- specialtyId debe identificar una especialidad activa.
- En PUT debe existir el médico activo.

GET admite pageSize, pageIndex, name y specialtyId.
specialtyId es un filtro adicional acordado para la integración.
Si name se proporciona, debe tener entre 3 y 100 caracteres.
Los filtros se combinan y los listados excluyen eliminados.

Entidad de respuesta:

```json
{
  "id": "33333333-3333-4333-8333-333333333333",
  "name": "Ana Pérez",
  "licenseNumber": "MP-12345",
  "specialty": {
    "id": "44444444-4444-4444-8444-444444444444",
    "name": "Cardiología"
  }
}
```

El nombre público es specialtyId y specialty, aunque el código interno
utilice Speciality.

Políticas acordadas:
- No eliminar un médico con citas BOOKED futuras.
- No cambiar su especialidad mientras existan esas reservas.
- Esos conflictos producen HTTP 409.
- La historia debe conservarse y permanecer consultable.

## 8. Planificación mensual y slots

Las reglas semanales del mes y los slots concretos son conceptos distintos.
GET de planificación devuelve reglas, no reconstrucciones a partir de slots.

POST y PUT /api/availabilities reciben:

```json
{
  "doctorId": "33333333-3333-4333-8333-333333333333",
  "days": [
    {
      "day": "Monday",
      "startTime": "09:00",
      "endTime": "12:00"
    }
  ]
}
```

- El médico debe existir y estar activo.
- Se trabaja sobre el mes actual del centro.
- startTime debe ser anterior a endTime.
- No se permiten solapamientos.
- Se generan bloques completos de 30 minutos.
- No se generan slots pasados ni en días no laborables.
- Los días no laborables se obtienen de configuración JSON.
- La entrada admite días en español o inglés, sin distinguir mayúsculas.
- Nacho documentará la representación normalizada de day antes de
  integrar N02, manteniendo compatibilidad con las entradas acordadas.

GET /api/doctors/{id}/availabilities devuelve:

```json
[
  {
    "id": "55555555-5555-4555-8555-555555555555",
    "day": "Monday",
    "startTime": "09:00",
    "endTime": "12:00"
  }
]
```

El ejemplo de day ilustra el formato de entrada admitido.
Si no hay planificación del mes actual, la respuesta es [].

POST y PUT devuelven la planificación resultante:

```json
{
  "doctorId": "33333333-3333-4333-8333-333333333333",
  "year": 2026,
  "month": 10,
  "days": [
    {
      "id": "55555555-5555-4555-8555-555555555555",
      "day": "Monday",
      "startTime": "09:00",
      "endTime": "12:00"
    }
  ]
}
```

El envoltorio doctorId/year/month/days es una adaptación acordada.

POST genera la planificación y los slots faltantes sin duplicarlos.
PUT reemplaza la planificación y los slots futuros no reservados.
Ambas operaciones deben preservar reservas e historia.

PUT con days: [] retira la atención futura libre del mes.
No elimina reservas ni historia.

## 9. Consulta de slots disponibles

Endpoint complementario:
GET /api/availabilities?doctorId={guid}

Acceso: Paciente.
Respuesta HTTP 200:

```json
[
  {
    "availabilitySlotId": "22222222-2222-4222-8222-222222222222",
    "doctorId": "33333333-3333-4333-8333-333333333333",
    "date": "2026-10-20",
    "startTime": "09:00",
    "endTime": "09:30"
  }
]
```

Solo devuelve slots disponibles y no pasados de un médico activo.
Sin slots reservables, devuelve [].

## 10. Citas

POST /api/appointments recibe:

```json
{
  "doctorId": "33333333-3333-4333-8333-333333333333",
  "availabilitySlotId": "22222222-2222-4222-8222-222222222222",
  "patient": {
    "dni": 30123456
  },
  "reason": "Consulta de control"
}
```

Validaciones:
- Médico activo y existente.
- Slot obligatorio, disponible y perteneciente al médico indicado.
- Inicio del turno no pasado.
- Paciente existente y correspondiente a la identidad autenticada.
- DNI obligatorio, de 7 a 10 dígitos en reserva y búsqueda.
- reason obligatorio, con un mínimo de 5 caracteres.
- Una misma disponibilidad no puede reservarse dos veces.

La diferencia entre DNI de login (7-8 dígitos) y reserva/búsqueda
(7-10 dígitos) proviene de la consigna y se conserva documentada.

DTO común de cita:

```json
{
  "appointmentsId": "11111111-1111-4111-8111-111111111111",
  "appointmentsStatus": "BOOKED",
  "availabilitySlotId": "22222222-2222-4222-8222-222222222222",
  "patient": {
    "dni": 30123456,
    "fullName": ""
  },
  "doctor": {
    "doctorId": "33333333-3333-4333-8333-333333333333",
    "name": "Ana Pérez",
    "specialty": {
      "specialtyId": "44444444-4444-4444-8444-444444444444",
      "name": "Cardiología"
    }
  },
  "date": "2026-10-20",
  "startTime": "09:00",
  "endTime": "09:30",
  "availableTime": "09:00-09:30",
  "reason": "Consulta de control"
}
```

Los identificadores y las fechas son ejemplos.
Las pruebas deben adaptarlos a su dataset y reloj controlado.

fullName se devuelve como cadena vacía si no existe.
El DTO amplía la respuesta mínima de la consigna para reutilizarla
en creación, consultas y cambios de estado.

Atención: los catálogos usan id; el DTO de cita usa appointmentsId,
doctorId y specialtyId en sus respectivas estructuras.

### Citas del paciente

GET /api/appointments/patient?dni={dni}

- El DNI debe corresponder al paciente autenticado.
- Devuelve un array del DTO común.
- Incluye únicamente citas propias BOOKED cuyo inicio no haya pasado.
- Excluye canceladas, atendidas, ausentes e históricas.
- Sin resultados, devuelve [].

### Consulta diaria

GET /api/appointments?date=YYYY-MM-DD&pageSize=10&pageIndex=0

- date es obligatorio.
- Admite status opcional.
- Devuelve una página del DTO común.
- Sin status, no se restringe a un único estado.

### Búsqueda combinada

GET /api/appointments/search

Filtros opcionales combinables:
- specialtyId: GUID.
- doctorId: GUID.
- dni: número de 7 a 10 dígitos.
- date: YYYY-MM-DD.
- status: BOOKED, CANCELLED, ATTENDED o NO_SHOW.
- pageSize y pageIndex.

Los identificadores son GUID aunque un ejemplo del PDF los indique
como number. Un status no reconocido produce HTTP 400.

### Cancelación

DELETE /api/appointments/{id}

- Solo puede cancelar el paciente propietario.
- La cita debe estar BOOKED.
- La cancelación y la liberación del slot son atómicas.
- Se conserva la historia y se excluye la cita de los turnos activos.
- Respuesta: HTTP 200, text/plain, cuerpo ok.
- Para una cita ajena puede utilizarse HTTP 404 para no revelar su existencia.

## 11. Estados atendido y ausente

Adaptación administrativa acordada:
PATCH /api/appointments/{id}/status

Acceso: Admin.

```json
{
  "status": "ATTENDED"
}
```

También admite NO_SHOW.

- Solo permite pasar de BOOKED a ATTENDED o NO_SHOW.
- Solo puede ejecutarse después del fin del turno.
- No habilita reapertura ni cambios arbitrarios entre estados.
- Un valor no admitido produce HTTP 400.
- Una transición incompatible con el estado o momento produce HTTP 409.
- Devuelve HTTP 200 con el DTO común de cita.

## 12. Logout

Endpoint complementario:
POST /api/auth/logout

- Requiere un JWT válido.
- Revoca únicamente el token enviado en esa solicitud.
- El controlador de la base inicial devuelve HTTP 200 mediante Ok("ok").
- E01 debe verificar y documentar el Content-Type y cuerpo efectivo
  por HTTP, incluyendo su negociación de contenido.
- No debe declararse validado el contrato HTTP de logout solo por
  inspeccionar el controlador.

Alcance acordado: revocación local en una instancia.
No se garantiza persistencia tras reinicios ni distribución entre instancias.
La retención de la revocación debe cubrir la vigencia efectiva del JWT,
incluida su tolerancia de expiración.

## 13. Errores

Formato:

```json
{
  "errorCode": "VALIDATION_ERROR",
  "message": "Uno o más datos son inválidos.",
  "details": [
    {
      "field": "pageSize",
      "issue": "Debe estar entre 1 y 100."
    }
  ]
}
```

errorCode y message son obligatorios.
details es opcional.
Los códigos específicos se centralizan en la capa transversal.

| HTTP | Significado |
|---|---|
| 400 | Entrada o formato inválido |
| 401 | Identidad ausente, inválida, expirada o revocada |
| 403 | Identidad válida sin permiso |
| 404 | Recurso o ruta inexistente |
| 405 | Método HTTP no permitido |
| 409 | Conflicto de estado o concurrencia |
| 415 | Tipo de contenido no admitido |
| 429 | Límite de solicitudes excedido |
| 500 | Error interno no previsto |

No convertir indiscriminadamente errores de conexión o implementación en 409.
No devolver secretos, tokens ni trazas internas en las respuestas.

Si PUT retira un slot antes de una reserva concurrente, la reserva debe
fallar con 404 o 409 y nunca dejar una cita creada.
Lucas documentará el resultado elegido al integrar L02.

## 14. Límites de solicitudes

Ventanas de 60 segundos y cola de espera cero:

| Política | Límite | Partición |
|---|---|---|
| Login administrativo | 5 | IP |
| Login de paciente | 10 | IP |
| Reserva | 5 | Paciente autenticado |
| General para restantes endpoints | 100 | Usuario o IP |

Los valores se obtienen de configuración.
Los rechazos devuelven 429 con el formato común y se registran en logs.

## 15. Base técnica compartida

G02 incorpora las siguientes interfaces y sus implementaciones.
Su disponibilidad para el equipo depende de integrar el PR conjunto
G01 + G02 en development.

### Reloj del centro

Ubicación: Dsw2026Tpi.Application/Interfaces/IClinicClock.cs.

```csharp
public interface IClinicClock
{
    DateTimeOffset GetCurrentLocalDateTime();
}
```

Implementación: ClinicClock, registrada como Singleton.
Fuente de tiempo: TimeProvider, reemplazable en pruebas.

Configuración:

```json
{
  "Clinic": {
    "TimeZoneId": "America/Argentina/Buenos_Aires"
  }
}
```

La resolución admite el identificador equivalente Argentina Standard Time.
Una configuración inválida impide el inicio de la API.

Cada operación toma un único instante y deriva de él la fecha y hora:

```csharp
var now = _clinicClock.GetCurrentLocalDateTime();
var today = DateOnly.FromDateTime(now.DateTime);
var currentTime = TimeOnly.FromDateTime(now.DateTime);
```

No usar now.LocalDateTime: convertiría el resultado a la zona del host.

Las pruebas del reloj verificaron cambios de día, mes y año,
los dos identificadores de zona y zonas locales simuladas.
La ejecución reportada corresponde al entorno Windows de Emilio.

### Alcance de escritura

Ubicación: Dsw2026Tpi.Domain/Interfaces.

```csharp
public interface IClinicalWriteScopeFactory
{
    Task<IClinicalWriteScope> BeginAsync(
        CancellationToken cancellationToken = default);
}

public interface IClinicalWriteScope : IAsyncDisposable
{
    Task CompleteAsync(
        CancellationToken cancellationToken = default);
}
```

Implementación: ClinicalWriteScopeFactory, registrada como Scoped.
Utiliza el mismo Dsw2026TpiDbContext scoped que los repositorios.

Configuración:

```json
{
  "ClinicalWrite": {
    "LockTimeoutMilliseconds": 5000
  }
}
```

El rango admitido es de 0 a 60000 milisegundos.
Cero solicita adquisición inmediata, sin esperar si está ocupado.
Este timeout limita la espera del bloqueo, no toda la transacción.

La fábrica:
- Exige SQL Server.
- Rechaza alcances o transacciones anidados.
- Rechaza cambios pendientes en el ChangeTracker al comenzar.
- Abre una transacción ReadCommitted.
- Adquiere sp_getapplock en modo Exclusive, propietario Transaction,
  principal public y recurso tpi:clinical-write.
- Parametriza el recurso y el timeout.
- Comprueba el código de retorno.
- Limpia el seguimiento previo después de adquirir el bloqueo para
  evitar que nuevas consultas reutilicen entidades rastreadas antes.
- CompleteAsync guarda cambios pendientes y confirma.
- DisposeAsync sin completar revierte y limpia el seguimiento.
- CompleteAsync solo admite un intento por alcance.

Los retornos de timeout y deadlock del bloqueo generan
ClinicalWriteConflictException, código CLINICAL_WRITE_CONFLICT,
que el middleware existente traduce a HTTP 409.

Los errores de conexión, parámetros o implementación no se convierten
indiscriminadamente en conflictos.

### Patrón de uso para los módulos

Ejemplo ilustrativo dentro de un servicio con dependencias inyectadas:

```csharp
await using var scope =
    await _writeScopeFactory.BeginAsync(cancellationToken);

var now = _clinicClock.GetCurrentLocalDateTime();

// Consultar nuevamente el estado dentro del alcance.
// Validar usando ese estado y el instante capturado.
// Realizar las escrituras mediante los repositorios.

await scope.CompleteAsync(cancellationToken);
```

Reglas de integración:
- Comenzar el alcance antes de las lecturas decisorias y modificaciones.
- No reutilizar objetos obtenidos antes del bloqueo para decidir cambios.
- Capturar el instante operativo después de adquirir el bloqueo.
- Una salida sin CompleteAsync provoca rollback.
- No continuar ni intentar confirmar después de un error de escritura.
- No abrir otra transacción dentro de los repositorios participantes.
- No ejecutar operaciones en paralelo sobre el mismo DbContext.
- DisposeAsync debe ejecutarse también después de CompleteAsync.

Los repositorios pueden llamar a SaveChangesAsync dentro del alcance;
la confirmación definitiva pertenece al alcance.

En la base actual, AppointmentPersistenceEf y
AvailabilityPersistenceEf todavía abren transacciones propias.
Lucas y Nacho deben adaptar esas operaciones al integrar sus módulos.

El registro de la fábrica no protege automáticamente las operaciones:
todas las mutaciones de catálogos, reglas, slots y citas deben consumir
el alcance compartido en sus tareas correspondientes.

Se conservan rowversion e índices únicos como defensas adicionales.

### Paginación compartida

PersistenceEf.Paginate conserva su firma pública.

- Valida pageSize entre 1 y 100 y pageIndex no negativo.
- Calcula el desplazamiento con long antes de convertirlo a int.
- Rechaza desplazamientos mayores que int.MaxValue.
- Ordena por sortOrder y luego por Id.
- Conserva filtros y exclusión de eliminados.
- Calcula total antes de paginar.
- Una página válida sin resultados devuelve data vacío.

Las consultas específicas de citas deben respetar el orden por fecha,
hora de inicio e Id definido en este contrato. El método genérico
no agrega por sí mismo campos de fecha u hora.

### Evidencia y límites de validación

Verificación local reportada por Emilio:
- Suite completa: 72 aprobadas, 0 fallidas, 0 omitidas.
- ClinicClockTests: 10 aprobadas.
- PersistenceEfTests: 10 aprobadas, incluidas las 2 preexistentes.

Las pruebas de persistencia actuales utilizan InMemory.

La exclusión mutua, el timeout, la liberación del bloqueo y el rollback
en SQL Server real quedan pendientes de la infraestructura de C01.
La compilación y los mocks no acreditan esas garantías.

## 16. Adaptaciones y límites del alcance

Adaptaciones acordadas respecto del detalle explícito del PDF:
- Valores predeterminados y límites del paginado.
- Filtro specialtyId en médicos.
- Envoltorio de respuesta para planificación mensual.
- Comportamiento de PUT con days vacío.
- Políticas de baja y reasignación con relaciones activas.
- DTO común de cita ampliado.
- Filtros status y operación PATCH de estados.
- Endpoint de slots y contrato de logout complementarios.
- Reloj del centro y alcance común de escritura.

Se mantienen fuera de este cierre:
- CRUD completo de pacientes.
- Licencias, fotos, consultorios y sedes.
- Dashboard adicional sin necesidad funcional.
- Facturación, historia clínica y videoconsultas.
- Obras sociales y notificaciones por email o SMS.
- Revocación distribuida o persistente y CI/CD.

Para el panel se reutilizan los totales paginados de catálogos y las citas
del día filtradas por BOOKED.

Todo cambio posterior de este contrato debe coordinarse con los
responsables afectados y registrarse antes de depender de él.