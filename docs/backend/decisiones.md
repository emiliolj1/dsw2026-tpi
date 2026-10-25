# Decisiones del cierre del backend

Fecha de registro inicial: 2026-10-07.
Actualización documental de E02: 2026-10-25.
Rama de integración: development.
Responsable de coordinación: Emilio.

Estas decisiones provienen de la planificación de cierre.
Su registro no implica implementación, prueba ni aprobación de PR.
Las evidencias se mantienen en seguimiento.md y el contrato público
se define en contrato-v17.md.

## D01 — Alcance y tecnología

Se mantiene ASP.NET Core sobre .NET 10, Entity Framework Core
y SQL Server, con la arquitectura en capas existente.

El entorno local de referencia es Windows con SQL Server LocalDB.
El frontend posterior utilizará React.

SDK inicial verificado en el equipo de Emilio: 10.0.302.
Runtime observado en las pruebas iniciales: .NET 10.0.10.
SDK informado en PC02 para el trabajo de E01/E02: 10.0.401.
Versiones verificadas mediante consulta local:
- Microsoft.AspNetCore.Identity.EntityFrameworkCore: 10.0.9.
- Microsoft.EntityFrameworkCore.SqlServer: 10.0.9.
- Microsoft.EntityFrameworkCore.Tools: 10.0.9.
- Microsoft.EntityFrameworkCore.InMemory, en pruebas: 10.0.9.
- Herramienta CLI dotnet-ef: 10.0.9.

Las versiones solicitadas y resueltas de estos paquetes coinciden.

La restricción de no actualizar paquetes correspondió al PR documental G01.
En E02 se agregó una referencia explícita a System.Security.Cryptography.Pkcs
9.0.21 en Data para corregir las alertas de la dependencia transitiva 9.0.4.
Se conservan EF Core 10.0.9 y Microsoft.Data.SqlClient 6.1.1.
La evidencia y las consultas de vulnerabilidades están en
evidencia-api-transversal.md; la corrección aún no está integrada.

## D02 — Ramas y dependencias

Los cambios se realizan en ramas por tarea, con PR hacia development.

G01 y G02 se integraron conjuntamente en development mediante
el PR #21, SHA de merge 11cd043.

Esta decisión reemplazó la integración separada originalmente prevista.
La base compartida está disponible para las tareas dependientes.

Una dependencia solo se considera disponible cuando está integrada
en development. Un PR abierto no es suficiente.

Cada tarea registra responsable, dependencias, rama, PR, SHA integrado
y evidencia en seguimiento.md.

## D03 — Protección de datos y base de pruebas

Se conservan los datos existentes.

C01 está integrada mediante PR #22, SHA de merge 1645370.
Las pruebas SQL utilizan bases nuevas y exclusivas,
separadas de la base de desarrollo.

Configuración: variable DSW2026_TEST_SQL_CONNECTION.
Dsw2026Tpi_Test_Config es el catálogo de configuración;
cada fixture genera Dsw2026Tpi_Test_<GUID>.

La creación, aislamiento y limpieza segura se documentan en
[pruebas-sql.md](pruebas-sql.md).
Se verificó la instancia LocalDB MSSQLLocalDB en Windows.
La limpieza valida el prefijo y el GUID del catálogo generado,
y rechaza conexiones arbitrarias y archivos adjuntos.

La fábrica no debe ejecutar limpieza destructiva sobre una base
de desarrollo.

Las pruebas de migración con datos se realizarán sobre una copia
o un dataset de prueba reproducible.

La recreación o eliminación de una base compartida requiere
una decisión específica del equipo.

## D04 — Responsabilidad sobre migraciones

Nacho es el único responsable de generar migraciones y snapshots
de los dos contextos.

Los demás integrantes le comunican los cambios de modelo necesarios.
No se generan migraciones paralelas desde otras tareas.

Se priorizan migraciones aditivas y preservación de datos.
N04 debe documentar y probar la estrategia completa de actualización.

## D05 — Fecha y hora del centro

Las reglas de agenda, reservas y estados utilizan la fecha y hora
del centro en Argentina, independientemente de la zona del equipo.

La auditoría técnica se registra en UTC.

G02 incorpora un reloj inyectable y configurable.
Cada operación tomará un único instante para sus comparaciones.

Identificadores previstos para resolver la zona:
- IANA: America/Argentina/Buenos_Aires.
- Windows: Argentina Standard Time.

Las pruebas de G02 verificaron ambos identificadores en el entorno
Windows de Emilio. La disponibilidad de la zona debe comprobarse
también al preparar otros entornos de ejecución.
No se usará la zona del host como sustitución silenciosa.

## D06 — URLs locales y CORS

Valores locales configurados para E02:
- API: http://localhost:5278.
- Frontend React: http://localhost:5173.

Cors:AllowedOrigins debe contener orígenes HTTP/HTTPS explícitos.
Una lista vacía o con valores inválidos impide el arranque.
No se admiten comodines, credenciales, rutas, consultas ni fragmentos.

E02 verificó localmente preflight con Authorization y Content-Type,
y solicitudes autenticadas desde navegador.
El origen 5173 permitió leer HTTP 200 del health-check.
El origen 5174 fue bloqueado por el navegador durante el preflight,
sin Access-Control-Allow-Origin.

Ese bloqueo del navegador no se documenta como un HTTP 403 de la API.
Las pruebas SQL verificaron además los headers y el HTTP 204 de OPTIONS.
E02 continúa pendiente de PR e integración.

Si cambian los puertos, deben actualizarse configuración,
contrato y documentación.

No se habilitará cualquier origen para resolver problemas de CORS.

## D07 — Contratos públicos

Se utilizan los nombres JSON definidos en contrato-v17.md.

Las correcciones de serialización no obligan a renombrar entidades
C# ni columnas de la base.

Los DELETE de médicos, especialidades y citas devolverán
HTTP 200, text/plain y cuerpo literal ok.

Las ampliaciones respecto del PDF se identifican como adaptaciones.
Los cambios posteriores se coordinan antes de que otro módulo
dependa de ellos.

## D08 — Planificación mensual

Las reglas del mes se persisten separadamente de los slots concretos.

PUT reemplaza la planificación y los slots futuros no reservados,
preservando reservas e historia.

PUT con days vacío retira la atención futura libre del mes.

Los feriados y días no laborables se obtienen de configuración JSON.
El calendario operativo debe verificarse antes del cierre.
Las fechas artificiales usadas en tests no se trasladan
automáticamente a la configuración operativa.

E02 coordina la configuración y N02 verifica su aplicación a la agenda.

Para E02 se acordó una base de feriados nacionales de Argentina de 2026,
con 17 fechas y revisión de fuentes oficiales al 2026-10-25.
Emilio coordina su actualización.
El alcance, las fuentes y las fechas están en
[calendario-operativo.md](calendario-operativo.md).

No se incluyen automáticamente días turísticos ni cierres provinciales,
municipales o propios del centro. Esos cierres requieren confirmación.
Antes de operar en 2027 debe actualizarse el calendario.
La regresión de E02 no acredita por sí sola la exclusión de esas fechas
en la nueva planificación mensual de N02.

## D09 — Escrituras clínicas y concurrencia

G02 incorpora un alcance transaccional compartido sobre el mismo
DbContext scoped utilizado por los repositorios.

Las mutaciones de catálogos, reglas, slots y citas adquirirán
la misma clave de bloqueo: tpi:clinical-write.

La implementación utiliza sp_getapplock en SQL Server.
C01 verificó confirmación, rollback y timeout entre conexiones
independientes mediante el PR #22, SHA de merge 1645370.

Las verificaciones de concurrencia entre módulos se completan
en sus tareas correspondientes y en C04.

El alcance se inicia antes de las lecturas decisorias.
Solo CompleteAsync confirma; Dispose sin completar revierte.

Se conservan rowversion e índices únicos como defensas adicionales.

C04 medirá el efecto de la serialización compartida sobre el rendimiento.

## D10 — Bajas, relaciones y estados

No se elimina una especialidad con médicos activos.

No se elimina un médico ni se cambia su especialidad mientras
tenga citas BOOKED futuras.

La historia debe permanecer consultable.

Se habilitará una operación administrativa para pasar de BOOKED
a ATTENDED o NO_SHOW después del fin del turno, conforme al contrato.

## D11 — Autenticación y revocación

El administrador inicial y los roles deben crearse idempotentemente.

El alta automática del paciente debe evitar duplicados y rechazar
combinaciones incompatibles de identidad.

La revocación JWT tiene alcance local, en una instancia.
No se garantiza persistencia tras reinicios ni distribución.

E01 está integrada mediante PR #27, SHA de merge a9d3930.
Sus pruebas verificaron login, acceso, logout,
rechazo del token revocado y expiración efectiva por HTTP.

Logout devuelve HTTP 200, text/plain y cuerpo literal ok.
La validación JWT utiliza ClockSkew igual a cero.

Identity y pacientes comparten conexión y transacción durante
el login del paciente, bajo el bloqueo SQL común de G02.

InitialAdmin:Email e InitialAdmin:Password deben permanecer
configurados. Una configuración incompleta impide iniciar la API.

La evidencia y el estado de integración se registran en
evidencia-autenticacion.md y seguimiento.md.

Los secretos se configuran localmente y no se incorporan a Git.

## D12 — Criterio de validación

La base inicial compiló y aprobó 54 pruebas, sin fallidas ni omitidas,
en el equipo de Emilio.

Ese resultado no sustituye las pruebas de SQL Server real
ni acredita el cierre completo del backend.

C01 proporciona la infraestructura SQL.
Cada integrante prueba su módulo.
C04 ejecuta la aceptación integrada y mide rendimiento.

Las pruebas de concurrencia utilizarán conexiones y DbContext
independientes. Los mocks no demuestran comportamiento del motor SQL.

Las pruebas pendientes u omitidas se registran como tales.
El objetivo de rendimiento es inferior a 3 segundos en las condiciones
de operación normal que C04 documente.

## D13 — Propiedad de archivos compartidos

| Área | Responsable |
|---|---|
| Program, DI, configuración, JWT y middleware | Emilio |
| Reloj, alcance transaccional y persistencia común | Emilio |
| Contrato, decisiones y seguimiento | Emilio |
| Modelo de reglas, migraciones y snapshots | Nacho |
| Agenda y planificación mensual | Nacho |
| CRUD y listados de médicos y especialidades | Charly |
| Infraestructura SQL y proyecto de pruebas | Charly |
| README, recorrido HTTP y aceptación integrada | Charly |
| Citas, reservas, cancelaciones y estados | Lucas |

En DoctorController, Charly modifica CRUD/listado y Nacho
la acción de planificación, coordinando sus cambios.

Charly revisa los PR de Emilio.
Emilio revisa los PR de los demás.
Nacho y Lucas revisan además la compatibilidad entre agenda y citas.

## D14 — Límites funcionales

Se incluyen la matrícula profesional licenseNumber y los campos
exigidos por el contrato.

La exclusión de "licencias" se refiere a gestionar ausencias
o licencias del médico, no a su matrícula profesional.

Quedan fuera:
- CRUD completo de pacientes.
- Gestión de licencias o ausencias médicas.
- Fotos, consultorios y sedes.
- Facturación e historia clínica.
- Videoconsultas y obras sociales.
- Avisos por email o SMS.
- Revocación distribuida o persistente.
- CI/CD.

El panel reutiliza totales paginados y las citas del día BOOKED.
No se crea un módulo adicional de estadísticas para elementos
decorativos del mockup.

## Pendientes de concreción

| Punto | Responsable | Momento |
|---|---|---|
| Confirmar y registrar la revisión de contratos e interfaces por el equipo | Emilio coordina | Coordinación de los módulos |
| Fijar representación normalizada de day en respuestas | Nacho, coordinado con Emilio | Antes de integrar N02 |
| Documentar respuesta ante reserva de un slot retirado | Lucas, coordinado con Nacho | L02 |
| Confirmar cierres adicionales del centro y comprobar su aplicación a la agenda | Emilio coordina con Nacho | N02 y aceptación C04 |
| Actualizar feriados y revisar nuevas disposiciones para el siguiente año | Emilio coordina | Antes de operar en 2027 |
| Documentar condiciones y resultados de rendimiento | Charly | C04 |