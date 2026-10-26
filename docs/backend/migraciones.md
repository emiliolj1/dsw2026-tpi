# Backend DSW2026 — migraciones y preservación de datos (N01/N04)

Repositorio: `emiliolj1/dsw2026-tpi`. Base del paquete: rama
`feature/nacho-reglas-mensuales` en `9589d27c5c6d7e4de7a0fe55622a2d56a1675006`.

## Modelo de agenda y datos anteriores (N01)

- `Availability` continúa representando un slot de 30 minutos; su `Id` NO se reemplaza.
- `AvailabilityRule` representa un rango semanal en el mes: médico, año, mes,
  día, inicio y fin. La tabla tiene índice único filtrado por `Deleted = 0`.
- `Availabilities.AvailabilityRuleId` es GUID nullable y referencia
  `AvailabilityRules.Id`, sin borrado en cascada (`Restrict`). Solo se permite
  `NULL` para slots históricos/legados; el nuevo flujo de agenda siempre crea
  los slots con ID de regla. No inferir reglas a partir de slots ambiguos.
- `20261025232313_AddMonthlyAvailabilityRules` es ADITIVA: incorpora columna,
  tabla, índices y FK. No renombra citas ni reemplaza IDs. La migración y su
  snapshot ya existen en la rama de base: no recrearlos ni pegar otra copia.
- Nunca reescribir una migración que ya haya sido aplicada en una base del equipo.

## Escenarios: decidir ANTES de aplicar

1. **Nueva/vacía:** crear una base NUEVA aislada, migrar `AuthenticationDbContext`
   y `Dsw2026TpiDbContext`; ejecutar `IdentitySeeder` real con configuración
   segura. Probar login real antes de declarar completado.
2. **Base que ya está en esquema actual:** hacer respaldo y restaurar una copia,
   ejecutar `scripts/migrations/preflight.sql` sobre ella. Aplicar solamente
   migraciones pendientes en la COPIA. Comparar conteos y vínculos antes/después.
3. **Esquema legado anterior a RebuildIdentityWithGuidKeys / LinkPatientToIdentityUser:**
   **BLOQUEO**. No ejecutar primero esas migraciones. Pueden eliminar/alterar
   identidad/columnas de pacientes. Exportar desde el ORIGEN intacto con un
   procedimiento aprobado los usuarios, roles, vínculos y pacientes. Migrar a
   una base nueva actualizada conservando GUID existentes y guardando el mapa
   antiguo→nuevo para IDs no GUID. Reconstruir `UserId` solo con asociaciones
   únicas comprobables. Casos ambiguos requieren decisión humana. No asignar
   `Guid.Empty` como vínculo para pacientes históricos. Hashes/credenciales:
   verificar formato compatible con Identity, o establecer recuperación de acceso.
4. **Base ya afectada por una migración destructiva:** una nueva migración NO
   recrea identidades ni datos anteriores; recurrir a respaldo/exportación
   válida y analizar la discrepancia, sin afirmar recuperación automática.

## Verificación reproducible en COPIA

1. Registrar identificación del SQL Server, DB, fecha, respaldo y SHA del repositorio.
2. En SSMS/ADS elegir manualmente la base de COPIA y ejecutar el preflight
   `scripts/migrations/preflight.sql` (solo lectura, sin revelar datos personales).
3. Registrar listas separadas para migraciones del contexto de aplicación y el
   de Identity; `__EFMigrationsHistory` puede incluir ambas si comparten catálogo.
4. Exportar conteos y controles de integridad. Verificar duplicados de DNI,
   UserId vacío, citas huérfanas, relaciones Doctor/Patient/Availability.
5. Aplicar en copia migraciones con `dotnet ef database update` indicando SIEMPRE
   un connection string de base de prueba. Especificar `--context`, `--project`
   y `--startup-project`; nunca confiar ciegamente en el catálogo por defecto.
6. Comparar conteos, IDs y estados `BOOKED` de reservas, `RowVersion` donde
   corresponda, fechas, citas y slots. No publicar datos identificables.
7. Probar endpoints protegidos, lectura de históricos, seeding y login real;
   registrar el resultado y la versión de herramientas en evidencia.
8. Mantener origen intacto hasta aceptación conjunta con Emilio, Lucas y Charly.

### Comandos modelo (reemplazar por entorno de pruebas real)

```bash
# Solo ejecutar sobre base de test identificada en ConnectionStrings__DefaultConnection.
ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations list \
  --context Dsw2026TpiDbContext \
  --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api

ASPNETCORE_ENVIRONMENT=Development dotnet ef migrations list \
  --context AuthenticationDbContext \
  --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api

# Luego de verificar el destino y tener respaldo restaurable:
ASPNETCORE_ENVIRONMENT=Development dotnet ef database update \
  --context Dsw2026TpiDbContext \
  --project Dsw2026Tpi.Data --startup-project Dsw2026Tpi.Api
```

**Nota:** el comando de `AuthenticationDbContext` necesita confirmar el
proyecto donde el contexto/migraciones están configurados (verificar en
`Program.cs`/`dotnet ef dbcontext list`); estos ejemplos no sustituyen esa
verificación. Las variables reales no se guardan en git.

## Comportamiento funcional v1.7 (N02/N03)

- `GET /api/doctors/{id}/availabilities`: reglas activas del mes actual, no
  inferencias basadas en slots.
- POST agrega reglas faltantes; regla idéntica no se duplica. Solapamientos se
  rechazan con 409. PUT reemplaza configuración y puede recibir `days: []`.
- Historia y citas reservadas sobreviven PUT; los slots futuros libres retirados
  quedan con baja lógica. Los slots nuevos referencian siempre una regla válida.
- Usa reloj del centro `IClinicClock` y alcance de escritura `IClinicalWriteScope`.
- El repositorio hace `SaveChanges` dentro de la transacción SQL compartida,
  con commit final exclusivamente en `CompleteAsync`. Un rollback deshace
  también esos SaveChanges intermedios; sirven para liberar índices filtrados.
- La serialización de agenda frente a citas requiere que el módulo de Lucas
  use la misma clave `tpi:clinical-write`. Verificación cruzada pendiente de L02.

## Faltantes de aceptación (no confundir código escrito con probado)

- Verificar el build y todas las pruebas en Windows con .NET 10 / SQL Server.
- Probar sobre una copia que contenga reservas anteriores a la migración N01.
- Correr `AvailabilitySqlTests` y `AvailabilityConcurrencySqlTests` con la fábrica C01.
- Verificar PUT/Reserva simultáneos tras integración real de L02; otros autores
  todavía pueden cambiar los archivos comunes.
- Cerrar N04 únicamente después de integradas E01 y L03 y firmada evidencia.

La documentación **no autoriza borrar/alterar** bases compartidas.
