# Evidencia del módulo de citas (L04)



Entorno: Windows, LocalDB (SQL Server 17.0), .NET 10, rama test/lucas-citas-cierre, commit 70d6ed2.



## Resultados

\- Sin SQL: 136/136

\- Suite SQL completa: 89/89, 0 omitidas



## Casos cubiertos

| Caso | Prueba | Resultado |

|---|---|---|

| Reserva simultánea del mismo slot | AppointmentConcurrencySqlTests | 1 BOOKED, otra 409 |

| Cancelar y reservar de nuevo | AppointmentConcurrencySqlTests | CANCELLED + BOOKED nueva |

| Doble cancelación y paciente ajeno | AppointmentConcurrencySqlTests | 409 y 404 |

| Rollback antes de CompleteAsync | AppointmentConcurrencySqlTests | Sin cita, slot Available |

| Error SQL inesperado | AppointmentServiceTests | Se propaga (500), no 409 |

| Reserva vs PUT de agenda | AgendaBookingConcurrencySqlTests (N03) | Reserva preservada |

| Estados ATTENDED/NO\_SHOW | AppointmentStatusSqlTests | 200; futuro 409; inválido 400; inexistente 404 |

| Cita cerrada no se cancela ni reabre | AppointmentStatusSqlTests | 409, slot sigue Booked |

| PATCH como paciente / sin token | AppointmentContractTests | 403 / 401 |

| Motivo corto, GUID vacío, médico inexistente, slot de otro médico | AppointmentServiceTests | 400 / 404 |

| Turno pasado (día, mes, hora anterior) con reloj fijo | AppointmentServiceTests | 400, no crea cita |

| Paginado con empates de fecha y hora | AppointmentSearchSqlTests | 5 de 5, sin repetidos, orden estable |



## Ejemplos JSON (sin datos reales)



Reserva (POST /api/appointments):

{"doctorId":"<GUID>","availabilitySlotId":"<GUID>","patient":{"dni":30123456},"reason":"Consulta de control"}



Cambio de estado (PATCH /api/appointments/{id}/status, admin):

{"status":"ATTENDED"}



Error de conflicto:

{"errorCode":"APPOINTMENT\_STATUS\_CONFLICT","message":"Solo se puede cambiar el estado de una cita BOOKED."}



## Pendientes de L04
- Sin pendientes conocidos. La prueba HTTP con token de login real (E01) quedó en AppointmentRealLoginSqlTests (reserva y cancelación). El historial tras la baja de médico y especialidad quedó en AppointmentSearchSqlTests (StillReturnsHistory).
