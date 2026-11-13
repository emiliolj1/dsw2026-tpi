# Integración del frontend con la API de turnos

## Conexión local

- API: `http://localhost:5278`.
- Swagger: `http://localhost:5278/swagger/index.html`.
- Origen frontend permitido por defecto: `http://localhost:5173`.
  Si el frontend usa otro origen, configurarlo en `Cors:AllowedOrigins`.

## Autenticación

| Rol | Endpoint | Cuerpo |
|---|---|---|
| Administrador | `POST /api/auth/admin/login` | `email`, `password` |
| Paciente | `POST /api/auth/patient/login` | `email`, `dni` numérico |

Los logins devuelven `token` y `role`. En solicitudes protegidas enviar
`Authorization: Bearer <token>`. Si una sesión vence o se revoca, un `401`
indica que el usuario debe volver a iniciar sesión.

## Recorrido principal

| Acción | Rol | Solicitud | Resultado |
|---|---|---|---|
| Consultar catálogos | Ambos | `GET /api/specialties` y `GET /api/doctors` | `200`, listados paginados |
| Crear planificación | Administrador | `POST /api/availabilities` | `201`; recibe `doctorId` y `days` con rangos `HH:mm` |
| Consultar planificación | Administrador | `GET /api/doctors/{doctorId}/availabilities` | `200`, array de reglas del mes vigente |
| Reemplazar planificación | Administrador | `PUT /api/availabilities` | `200`; `days: []` retira los turnos libres futuros y conserva reservas e historia |
| Consultar turnos libres | Paciente | `GET /api/availabilities?doctorId={id}` | `200`, lista de turnos; `[]` si no hay ninguno |
| Reservar | Paciente | `POST /api/appointments` | `201`, cita `BOOKED` |
| Consultar mis citas | Paciente | `GET /api/appointments/patient?dni={dni}` | `200`, citas activas del paciente |
| Buscar citas | Administrador | `GET /api/appointments/search` | `200`, resultados paginados en `data` |
| Cancelar | Paciente | `DELETE /api/appointments/{appointmentsId}` | `200`, texto `ok`; volver a consultar los turnos libres |
| Cerrar sesión | Ambos | `POST /api/auth/logout` | `200`, texto `ok`; descartar el token usado |

Ejemplo del cuerpo de una reserva. Reemplazar los identificadores con
los devueltos por la API y usar el DNI del paciente autenticado:

```json
{
  "doctorId": "ID_DEL_MEDICO",
  "availabilitySlotId": "ID_DEL_TURNO",
  "patient": { "dni": 44444444 },
  "reason": "Consulta de demostración"
}
```

En planificación, las respuestas de POST y PUT tienen `doctorId`, `year`,
`month` y `days`; el GET administrativo devuelve el array de reglas
directamente. Cada regla tiene su propio `id` y devuelve `day` en español
en mayúsculas (por ejemplo, `MIÉRCOLES`). La entrada admite también inglés.

- `specialtyId`: especialidad; se usa al crear o filtrar médicos.
- `doctorId`: médico; se usa para consultar su disponibilidad.
- `availabilitySlotId`: turno libre; se envía al reservar.
- `appointmentsId`: cita creada; se usa al cancelar.
- `appointmentsStatus`: estado de la cita. Una reserva nueva queda `BOOKED`.

## Manejo de errores

Leer `errorCode` y `message` del JSON de error. Usar `details` para
mostrar errores por campo cuando esté presente. Si una reserva falla con
`404 ENTITY_NOTFOUND` porque el turno se retiró, o con `409` porque ya no
está disponible, refrescar la lista antes de ofrecer otro intento. Ante un `429`,
informar el límite de solicitudes y evitar reintentos inmediatos.

Ver el [contrato v1.7](contrato-v17.md) y el
[recorrido de ejemplo](../../Dsw2026Tpi.Api/Dsw2026Tpi.Api.http).
