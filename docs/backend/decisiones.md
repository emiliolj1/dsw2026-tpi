# Decisiones del cierre del backend

Fecha de registro: 2026-10-07.
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

SDK verificado en el equipo de Emilio: 10.0.302.
Runtime observado durante las pruebas: .NET 10.0.10.
Versiones verificadas mediante consulta local:
- Microsoft.AspNetCore.Identity.EntityFrameworkCore: 10.0.9.
- Microsoft.EntityFrameworkCore.SqlServer: 10.0.9.
- Microsoft.EntityFrameworkCore.Tools: 10.0.9.
- Microsoft.EntityFrameworkCore.InMemory, en pruebas: 10.0.9.
- Herramienta CLI dotnet-ef: 10.0.9.

Las versiones solicitadas y resueltas de estos paquetes coinciden.

No actualizar paquetes ni herramientas como parte del PR documental G01.

## D02 — Ramas y dependencias

Los cambios se realizan en ramas por tarea, con PR hacia development.

G01 debe integrarse antes de G02.
G02 proporciona la base compartida que necesitan los demás integrantes.

Una dependencia solo se considera disponible cuando está integrada
en development. Un PR abierto no es suficiente.

Cada tarea registra responsable, dependencias, rama, PR, SHA integrado
y evidencia en seguimiento.md.

## D03 — Protección de datos y base de pruebas

Se conservan los datos existentes.

Las pruebas SQL utilizarán bases nuevas, exclusivas de pruebas,
separadas de la base de desarrollo.

Convención propuesta para C01:
Dsw2026Tpi_Tests_<identificador_unico>.

Charly implementará la creación, aislamiento y limpieza segura
de esas bases en C01 y documentará su configuración.

La fábrica no debe ejecutar limpieza destructiva sobre una base
de desarrollo ni sobre una conexión arbitraria.

La instancia y configuración efectiva de pruebas se verificarán
durante C01. Esta decisión no declara creada ninguna base.

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

G02 implementará un reloj inyectable y configurable.
Cada operación tomará un único instante para sus comparaciones.

Identificadores previstos para resolver la zona:
- IANA: America/Argentina/Buenos_Aires.
- Windows: Argentina Standard Time.

G02 verificará su resolución en el sistema donde se ejecute.
No se usará la zona del host como sustitución silenciosa.

## D06 — URLs locales y CORS

Valores previstos:
- API: http://localhost:5278.
- Frontend React: http://localhost:5173.

E02 configurará y probará el origen explícito del frontend,
incluyendo las solicitudes preflight necesarias.

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

## D09 — Escrituras clínicas y concurrencia

G02 implementará un alcance transaccional compartido sobre el mismo
DbContext scoped utilizado por los repositorios.

Las mutaciones de catálogos, reglas, slots y citas adquirirán
la misma clave de bloqueo: tpi:clinical-write.

La propuesta utiliza sp_getapplock en SQL Server:
- Modo Exclusive.
- Propietario Transaction.
- Recurso y timeout parametrizados.
- Código de retorno comprobado.

El alcance se inicia antes de las lecturas decisorias.
Solo CompleteAsync confirma; Dispose sin completar revierte.

Se conservan rowversion e índices únicos como defensas adicionales.
Un bloqueo únicamente en memoria no acredita coordinación
entre procesos.

Esta serialización conservadora simplifica la consistencia
del cierre académico. C04 medirá su efecto sobre el rendimiento.

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

E01 verificará el ciclo login, acceso, logout y rechazo del token,
además del contrato HTTP efectivo de logout.

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
| Revisar contratos e interfaces con el equipo | Emilio coordina | Antes de integrar G01 |
| Verificar resolución de zona horaria y opciones del alcance | Emilio | G02 |
| Documentar instancia y configuración segura de pruebas SQL | Charly | C01 |
| Fijar representación normalizada de day en respuestas | Nacho, coordinado con Emilio | Antes de integrar N02 |
| Documentar respuesta ante reserva de un slot retirado | Lucas, coordinado con Nacho | L02 |
| Verificar cuerpo y Content-Type de logout por HTTP | Emilio | E01 |
| Verificar calendario operativo y responsable de actualización | Emilio coordina | E02 y aceptación C04 |
| Documentar condiciones y resultados de rendimiento | Charly | C04 |