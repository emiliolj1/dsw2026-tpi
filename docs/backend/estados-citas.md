\# Estados de citas: ATTENDED y NO\_SHOW (L03)



Adaptación acordada en G01: el PDF define los estados pero no la ruta.



\*\*PATCH /api/appointments/{id}/status\*\* (solo ADMINISTRADOR)



Request: `{"status": "ATTENDED"}` o `{"status": "NO\_SHOW"}`

Response 200: DTO común de cita con el estado actualizado.



| Situación | HTTP |

|---|---|

| Estado distinto de ATTENDED/NO\_SHOW (inventado, CANCELLED, BOOKED, nulo) | 400 |

| Cita inexistente | 404 |

| Cita que no está BOOKED (cancelada, atendida o ausente) | 409 |

| Turno que todavía no terminó (hora del centro) | 409 |

| Paciente autenticado | 403 |

| Sin token | 401 |



\- Solo se pasa de BOOKED a ATTENDED o NO\_SHOW, una vez terminado el intervalo del turno.

\- El slot NO se libera: queda Booked como historia.

\- Corre dentro del alcance de escritura clínica (rollback si falla).

\- La cancelación del paciente sigue en su DELETE específico.

