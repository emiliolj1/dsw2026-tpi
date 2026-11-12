# Rendimiento del backend — C04

Medición local del 12/11/2026 en Windows, con SQL Server LocalDB
(`MSSQLLocalDB`) y runtime .NET 10.0.13. Rama:
`test/charly-aceptacion-backend`, sobre `development` en `102e50e`
más los cambios locales de C04.

La prueba `BackendPerformanceSqlTests` creó una base aislada con
20 especialidades, 100 médicos, 3.200 turnos y 1.000 citas sintéticas.
Se hicieron 3 solicitudes de calentamiento y se midieron 20 solicitudes
secuenciales por consulta, con autenticación y límites de solicitudes activos.

| Consulta | Muestras | Mediana (ms) | p95 (ms) | Máximo (ms) | Fallos HTTP |
|---|---:|---:|---:|---:|---:|
| Especialidades | 20 | 23,03 | 131,81 | 137,31 | 0 |
| Médicos | 20 | 24,20 | 149,84 | 153,74 | 0 |
| Turnos libres | 20 | 27,91 | 171,97 | 174,18 | 0 |
| Mis citas | 20 | 27,98 | 180,15 | 193,13 | 0 |
| Búsqueda administrativa | 20 | 47,17 | 205,05 | 238,16 | 0 |

Resultado: las 100 consultas medidas devolvieron HTTP 200. El máximo
observado fue de 238,16 ms, por debajo del objetivo de 3.000 ms.

El tiempo se midió desde el envío de cada solicitud hasta recibir el
cuerpo completo. Se excluyeron el arranque, las migraciones, la carga
de datos, el login y el calentamiento. El resultado corresponde al
host de pruebas local con ese volumen de datos; no mide tráfico
concurrente ni latencia de red en producción.

Con LocalDB iniciado y `DSW2026_TEST_SQL_CONNECTION` configurada:

```bash
dotnet test Dsw2026Tpi.Tests/Dsw2026Tpi.Tests.csproj --no-restore \
  --filter "Purpose=Performance" --nologo \
  --logger "console;verbosity=detailed" \
  --logger "trx;LogFileName=c04-rendimiento.trx"
```