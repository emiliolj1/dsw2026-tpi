\# Evidencia del módulo de citas (L04)



Entorno: Windows, LocalDB (SQL Server 17.0), .NET 10, rama test/lucas-citas-cierre, commit <SHA>.



\## Resultados

\- Sin SQL: <N>/<N>

\- Suite SQL completa: <N>/<N>, 0 omitidas



\## Casos cubiertos

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



\## Ejemplos JSON (sin datos reales)



Reserva (POST /api/appointments):

{"doctorId":"<GUID>","availabilitySlotId":"<GUID>","patient":{"dni":30123456},"reason":"Consulta de control"}



Cambio de estado (PATCH /api/appointments/{id}/status, admin):

{"status":"ATTENDED"}



Error de conflicto:

{"errorCode":"APPOINTMENT\_STATUS\_CONFLICT","message":"Solo se puede cambiar el estado de una cita BOOKED."}



\## Pendiente

\- Prueba por HTTP con tokens emitidos por login real (depende de E01 integrada; la hace Charly en C04).

