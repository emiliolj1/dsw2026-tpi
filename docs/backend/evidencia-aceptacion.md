# Evidencia de aceptación del backend — C04

Validación local del 12/11/2026 en Windows y SQL Server LocalDB.
Rama: `test/charly-aceptacion-backend`. Base de `development`: `102e50e`.

- **Recorrido por HTTP:** administrador y paciente iniciaron sesión por
  los endpoints reales. El administrador creó la especialidad, el médico
  y la agenda. El paciente reservó, canceló y volvió a reservar el mismo
  turno. La búsqueda administrativa conservó dos citas distintas: la
  primera `CANCELLED` y la segunda `BOOKED`.
- **Baja de médico frente a una reserva concurrente:** en la corrida
  registrada se confirmó primero la baja (`200`); la reserva recibió
  `404 ENTITY_NOTFOUND`. La consulta directa a SQL confirmó el médico
  inactivo con baja lógica, el turno sin reservar y ninguna cita creada.
- **Contrato v1.7:** se actualizó `AppointmentFlowIntegrationTests` para
  usar `AppointmentsId` y `AppointmentsStatus` y se retiraron los alias
  temporales del DTO. El flujo anterior y los dos casos nuevos pasaron.

| Validación | Aprobadas | Fallidas | Omitidas | Evidencia local |
|---|---:|---:|---:|---|
| Regresión sin SQL | 136 | 0 | 0 | `c04-regresion-no-sql.trx` |
| Regresión con SQL Server | 93 | 0 | 0 | `c04-regresion-sql.trx` |
| Rendimiento, ejecución separada | 1 | 0 | 0 | `c04-rendimiento.trx` |

Los dos casos nuevos de aceptación están incluidos en las 93 pruebas SQL.
El caso de rendimiento se agregó después de la regresión y se ejecutó
por separado. Ver [rendimiento.md](rendimiento.md).

La compilación del caso de rendimiento reportó una advertencia
`xUnit2031` preexistente en `AppointmentConcurrencySqlTests.cs`.