# Cierre del backend — E03

Responsable: Emilio. Base de revisión: `development` en
[`dd89963`](https://github.com/emiliolj1/dsw2026-tpi/commit/dd89963c82af5c226ee72ee53652f898a70a7ce0).
Las dependencias de E03 ya están integradas. El estado de cada tarea se
registra en [seguimiento.md](seguimiento.md).

| Verificación | Código o entorno verificado | Integración y evidencia |
|---|---|---|
| Autenticación y API transversal | E01: 112 pruebas sin SQL y 46 SQL; E02: 116 sin SQL y 62 SQL. | [E01](evidencia-autenticacion.md), [PR #27](https://github.com/emiliolj1/dsw2026-tpi/pull/27) (`a9d3930`); [E02](evidencia-api-transversal.md), [PR #28](https://github.com/emiliolj1/dsw2026-tpi/pull/28) (`40a114c`). |
| Preservación al migrar | `af360f1`: 136 pruebas sin SQL y 90 SQL, sin fallos ni omisiones. En una copia sintética restaurada se preservaron IDs, vínculos, estados y RowVersion; la cita seguía disponible por HTTP tras migrar. | [N04](evidencia-migraciones.md), [PR #34](https://github.com/emiliolj1/dsw2026-tpi/pull/34) (`af360f1`); evidencia integrada en [PR #35](https://github.com/emiliolj1/dsw2026-tpi/pull/35) (`d14590d`). |
| Aceptación integrada | Rama de C04 sobre `102e50e`: 136 pruebas sin SQL y 93 SQL; luego 1 prueba de rendimiento por separado. Sin fallos ni omisiones. Se verificaron reserva, cancelación, nueva reserva y la carrera entre baja de médico y reserva. | [C04](evidencia-aceptacion.md), [PR #37](https://github.com/emiliolj1/dsw2026-tpi/pull/37) (`9cc66b5`). |
| Rendimiento | LocalDB: 100 consultas secuenciales autenticadas, todas HTTP 200. Máximo: **238,16 ms**, por debajo del objetivo de **3.000 ms** para esa corrida. | [Entorno, datos y resultados](rendimiento.md). |
| Entrega | Checkout limpio de `68c0e7c`: compilación, arranque, Swagger y recorrido manual de integración. | [C05](#entrega-c05), [PR #38](https://github.com/emiliolj1/dsw2026-tpi/pull/38) (`dd89963`). |

Las cifras corresponden a corridas distintas y contienen pruebas repetidas
entre etapas. El total de C04 es 230 contando su ejecución separada de
rendimiento. El caso específico de N04 ya está incluido en sus 90 pruebas SQL.

## Entrega C05

El registro de consola de la verificación en `68c0e7c` muestra que:

- La solución compiló y la aplicación arrancó con Swagger. El historial de
  migraciones del contexto clínico incluía `20261025232313_AddMonthlyAvailabilityRules`.
- Ambos logins respondieron `200`; las altas de especialidad, médico y
  planificación respondieron `201`.
- La consulta de turnos libres respondió `200`, la reserva `201` y las
  consultas de paciente y administrador `200`. La cancelación devolvió
  `200` y se volvió a consultar la disponibilidad.
- El logout respondió `200` y la consulta siguiente con el mismo token
  devolvió `401`, como se esperaba.

Para repetir el recorrido, usar el [archivo HTTP](../../Dsw2026Tpi.Api/Dsw2026Tpi.Api.http)
y la [guía de integración frontend](integracion-frontend.md) sobre datos
de desarrollo. Los identificadores, claves y tokens de cada instalación
se completan localmente.

## Límites y seguimiento operativo

- **Calendario:** las 17 fechas acordadas para 2026 coinciden con
  `NonWorkingDays:Dates`. Queda por confirmar si el centro tiene cierres
  adicionales y comprobar con una prueba específica que la agenda excluye
  una fecha operativa configurada. Actualizar el calendario antes de
  generar agenda de 2027. Ver [calendario operativo](calendario-operativo.md).
- **Migraciones:** N04 parte de Identity con claves GUID ya normalizadas.
  Una conversión desde claves string requiere validación propia.
- **Rendimiento:** el resultado corresponde al conjunto de datos y al
  entorno local de C04; no cubre tráfico concurrente ni producción.
- **Autenticación:** la revocación del JWT es local a la instancia y se
  pierde al reiniciar. Ver [contrato v1.7](contrato-v17.md).

E03 consolida documentación y ejemplos HTTP. Para cerrar la entrega,
revisar el PR con Charly, fusionarlo en `development` y registrar el
PR y el SHA de merge en [seguimiento.md](seguimiento.md).
