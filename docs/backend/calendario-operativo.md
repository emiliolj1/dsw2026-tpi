# Calendario operativo — Base nacional 2026

## Alcance y responsabilidad

- Fecha de revisión de fuentes: 2026-10-25.
- Configuración: NonWorkingDays:Dates en Dsw2026Tpi.Api/appsettings.json.
- Zona operativa: Argentina, según el reloj configurable del centro.
- Cobertura de esta lista: 2026.
- Responsable de coordinación y actualización: Emilio.
- Aplicación a la agenda: Nacho, en N02.
- Aceptación integrada: Charly, en C04.

Se acordó cargar feriados nacionales de Argentina.
La lista no implica que estén confirmados todos los cierres del centro.
Debe contrastarse con la configuración antes de integrar E02.

## Fechas acordadas

| Fecha | Motivo |
|---|---|
| 2026-01-01 | Año Nuevo |
| 2026-02-16 | Carnaval |
| 2026-02-17 | Carnaval |
| 2026-03-24 | Memoria por la Verdad y la Justicia |
| 2026-04-02 | Veterano y Caídos en Malvinas |
| 2026-04-03 | Viernes Santo |
| 2026-05-01 | Día del Trabajador |
| 2026-05-25 | Revolución de Mayo |
| 2026-06-15 | Güemes, trasladado del 17 de junio |
| 2026-06-20 | Belgrano |
| 2026-07-09 | Independencia |
| 2026-08-17 | San Martín |
| 2026-10-12 | Diversidad Cultural |
| 2026-11-09 | Feriado nacional extraordinario, Decreto 1103/2026 |
| 2026-11-23 | Soberanía Nacional, trasladado del 20 de noviembre |
| 2026-12-08 | Inmaculada Concepción |
| 2026-12-25 | Navidad |

Total acordado: 17 fechas.
Se mantienen también los feriados que caen en fin de semana.

## Fuentes oficiales

- [Calendario nacional](https://www.argentina.gob.ar/feriados).
- [Cancillería: sección FERIADOS EN ARGENTINA 2026](https://enoru.cancilleria.gob.ar/es/node/84772).
  Se utilizó la sección argentina, excluyendo las listas de Noruega e Islandia.
- [Decreto 1103/2026, Boletín Oficial del 28/09/2026](https://www.boletinoficial.gob.ar/detalleAviso/primera/348022/20260928?busqueda=1).
  Su artículo 2 establece el feriado nacional del 9 de noviembre.
  Los días 10 y 11 tienen alcance territorial específico y no se
  incorporan como feriados nacionales generales.

## Cierres que requieren confirmación

No se incorporaron automáticamente los días turísticos
2026-03-23, 2026-07-10 y 2026-12-07.
Su eventual adopción como cierre del centro debe confirmarse.

Tampoco se incluyen automáticamente feriados provinciales, municipales,
asuetos ni cierres particulares.
Emilio debe coordinar esa confirmación y registrar los cambios.

Las fechas artificiales usadas en tests no se trasladan al calendario real.

## Mantenimiento y verificación

- Revisar nuevas disposiciones oficiales antes de generar planificación.
- Mantener sincronizados configuración y documento mediante PR.
- N02 debe probar que no se generan slots en las fechas excluidas.
- C04 debe verificar el calendario operativo integrado.
- Cargar y revisar 2027 antes de generar planificación de ese año.
- La ausencia de fechas de otro año no significa que ese año no tenga feriados.

Las 178 pruebas aprobadas de E02 no sustituyen esas verificaciones
de la nueva planificación mensual.
