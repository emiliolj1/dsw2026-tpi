# Evidencia de migraciones — DSW2026 (plantilla de cierre N04)

> Estado: **PENDIENTE DE EJECUCIÓN EN ENTORNO LOCAL**. No se registran resultados
> de pruebas como exitosos hasta obtener las salidas reales.

## Identificación y respaldo

- Fecha y responsable: PENDIENTE
- Rama y SHA de base: `feature/nacho-reglas-mensuales`, `9589d27`
- SHA de implementación integrada: PENDIENTE
- SQL Server versión/instancia, sin credenciales: PENDIENTE
- Nombre de base de PRUEBAS (no compartida): PENDIENTE
- Tipo de escenario: nueva / esquema actual con datos / legado / bloqueado
- Respaldo tomado y restauración verificada: PENDIENTE

## Inventario antes/después

| Indicador | Antes | Después | Resultado |
|---|---:|---:|---|
| Doctores | — | — | Pendiente |
| Pacientes | — | — | Pendiente |
| Disponibilidades | — | — | Pendiente |
| Citas totales | — | — | Pendiente |
| Citas BOOKED | — | — | Pendiente |
| Usuarios Identity | — | — | Pendiente |
| Duplicados UserId/DNI | — | — | Pendiente |
| Citas sin Availability | — | — | Pendiente |
| Integridad IDs cita/slot | — | — | Pendiente |

## Verificaciones específicas

- [ ] Esquema nuevo: `AvailabilityRules` y `AvailabilityRuleId` con FK Restrict.
- [ ] Misma cantidad e IDs de reservas del origen.
- [ ] Citas BOOKED no desaparecen al aplicar la migración N01.
- [ ] `AvailabilityRuleId` de slots anteriores conserva `NULL`, sin inventar reglas.
- [ ] Tests unitarios del servicio y contratos HTTP.
- [ ] SQL: POST/POST y PUT/PUT desde contextos distintos.
- [ ] SQL: rollback ante fallo intermedio.
- [ ] SQL: PUT/reserva con L02 integrado.
- [ ] Autenticación/Identity: login real y relaciones restauradas.
- [ ] Copia permite restauración, origen inalterado.

## Dependencias y bloqueos

- N03: depende de L02, fecha de integración/SHA por verificar.
- N04: depende además de E01 y L03; por verificar.
- Migración histórica de identidad: BLOQUEADA hasta conocer estado y respaldo
  de cada base legada. No automatizar mapeos por email dudoso.

## Evidencias adjuntas

Guardar localmente `dotnet build`, TRX y resultados preflight sanitizados.
No versionar secretos, nombres de pacientes ni tokens.

- PR N01: pendiente
- PR N02: pendiente
- PR N03: pendiente
- PR N04: pendiente
- Firma de aceptación de equipo: pendiente
