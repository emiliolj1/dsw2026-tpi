# N03 — Concurrencia y atomicidad de agenda

## Alcance

Se agregan pruebas de integración sobre SQL Server sin modificar los servicios
compartidos. `AvailabilityService.Update` y `AppointmentService.Create` ya usan
`IClinicalWriteScopeFactory` con la clave `tpi:clinical-write`. Se verifica la
integración cruzada sin atribuir cambios al código de otros integrantes.

## Pruebas nuevas

Archivo: `Dsw2026Tpi.Tests/Integration/AgendaBookingConcurrencySqlTests.cs`.

1. `ReservaConfirmada_YLuegoPutVacio_ConservaCitaYSuSlot`: la reserva
   confirmada sobrevive a PUT con `days: []`; la regla puede pasar a baja lógica,
   pero la cita y su slot siguen reservados.
2. `PutVacio_YReservaEnParalelo_NuncaPierdenUnaReservaConfirmada`: dos servicios,
   scopes y conexiones SQL diferentes, arrancan mediante la misma señal. Se
   aceptan los dos órdenes de ejecución serializados: reserva exitosa y slot
   preservado, o PUT exitoso y reserva rechazada. Un error 409 por bloqueo
   también es válido; no se acepta una cita BOOKED con slot eliminado.
3. `PutHttp_ConBloqueoOcupado_Responde409ConCodigoDeConflicto`: mantiene un
   `sp_getapplock` abierto en una conexión y comprueba que PUT por HTTP recibe
   `409` con `errorCode=CLINICAL_WRITE_CONFLICT` al vencer el timeout.

Las pruebas de servicio usan un reloj fijo (2030-06-03 08:00, UTC-03) para que
el resultado no dependa de la fecha de ejecución. La prueba HTTP utiliza el
reloj normal, sin depender de que existan horarios futuros.

## Cómo ejecutarlas en Windows / Git Bash

**Requisitos:** .NET SDK 10, una instancia SQL Server de pruebas disponible y
un usuario SQL con permiso para CREAR y ELIMINAR bases de prueba. No usar una
base compartida ni de producción.

Ejemplo con SQL Server local y autenticación de Windows (ajustar servidor):

```bash
export DSW2026_TEST_SQL_CONNECTION='Server=localhost;Database=Dsw2026Tpi_Test_Template;Trusted_Connection=True;TrustServerCertificate=True'

dotnet restore Dsw2026Tpi.slnx
dotnet build Dsw2026Tpi.slnx --no-restore

dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj \
  --no-build --filter 'FullyQualifiedName~AgendaBookingConcurrencySqlTests' \
  --logger 'console;verbosity=normal'
```

`SqlServerTestConnection` crea una base con nombre aleatorio y prefijo
`Dsw2026Tpi_Test_` y la elimina al terminar. No introducir nombres ni
credenciales reales en el repositorio. En SQL Server con autenticación SQL,
configurar la variable solo en tu equipo, fuera de Git.

Ejecutar también la regresión de agenda, reservas y transacciones:

```bash
dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj \
  --no-build --filter 'FullyQualifiedName~AvailabilityConcurrencySqlTests|FullyQualifiedName~AppointmentConcurrencySqlTests|FullyQualifiedName~ClinicalWriteScopeSqlTests|FullyQualifiedName~AvailabilitySqlTests' \
  --logger 'console;verbosity=normal'
```

## Evidencia que falta completar tras la ejecución

- [ ] `dotnet build` finalizó correctamente (fecha, SHA, versión de SDK).
- [ ] Las 3 pruebas nuevas finalizaron correctamente sobre SQL Server real.
- [ ] La regresión SQL indicada finalizó correctamente.
- [ ] Se guardó un resumen de los resultados sin contraseñas, tokens ni datos
      personales (solo conteos, resultados y errores sanitizados).
- [ ] El PR se abrió desde `fix/nacho-agenda-concurrente` hacia `development`.
- [ ] Un revisor aprobó y se realizó el merge (no afirmar antes de confirmarlo).

**Estado al generar este archivo:** código agregado, ejecución real pendiente.
No se hizo ninguna operación sobre una base compartida.
