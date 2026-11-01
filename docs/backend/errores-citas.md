\# Errores de reserva y cancelación de citas (L02)



| Situación | HTTP | Notas |

|---|---|---|

| Slot eliminado o retirado por un PUT de agenda | 404 | No se crea cita. |

| Slot bloqueado o ya reservado | 409 | No se crea cita. |

| Dos reservas simultáneas del mismo slot | 409 | Una sola cita BOOKED. |

| Conflicto de concurrencia o unicidad | 409 | Error esperado de la reserva. |

| Cancelar una cita que no está BOOKED | 409 | Solo se cancela BOOKED. |

| Cancelar una cita de otro paciente | 404 | No se revela su existencia. |

| Otro error de base de datos | 500 | No se disfraza de 409. |



Reserva y cancelación corren dentro de IClinicalWriteScope: si algo falla

antes de CompleteAsync, todo se revierte. Los logs llevan solo IDs.

