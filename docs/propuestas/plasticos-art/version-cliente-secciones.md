<!-- s1 -->
## Resumen

El equipo de desarrollo construirá para Plásticos ART un sistema de control de producción en ocho módulos y lo entregará por partes, en ocho semanas. El sistema cubre fórmulas, pedidos, órdenes de producción, operarios por turno, inventario de materia prima, cálculo de materia prima por orden, registro de pesos con balanza y reportes.

Funciona dentro de la fábrica, sin internet obligatorio. Se reutilizan componentes ya probados en planta en otro sistema de pesaje del mismo equipo: lectura de balanza, seguridad y reportes.

La primera entrega utilizable llega en la semana 4 (módulos 1 a 5). La balanza real se integra en la semana 6 y los reportes en la semana 7. El lunes 2027-01-04 el sistema pasa a ser el registro oficial, con acompañamiento en planta. La entrega final y su acta se firman el viernes 2027-01-08.

Las fechas parten del arranque del lunes 2026-11-16. Si la firma o los insumos de la empresa se demoran, todo el calendario se desplaza día por día.

La inversión total es de 20 000 Bs, en tres pagos. Incluye 90 días de soporte. No incluye balanza, PCs, servidor, hosting ni licencias de terceros.

| Hito | Fecha | Condición | Monto |
|---|---|---|--:|
| 1. Firma | Antes del lun 2026-11-16 | Firma de la propuesta y designación del responsable del proyecto | 30 % = 6 000 Bs |
| 2. Primera entrega parcial | Vie 2026-12-11 (aprobación tácita: mié 2026-12-16) | Módulos 1 a 5 instalados, con datos maestros reales y pruebas de aceptación críticas aprobadas | 40 % = 8 000 Bs |
| 3. Entrega final y puesta en marcha | Vie 2027-01-08 (aprobación tácita: mié 2027-01-13) | Sistema instalado, pruebas de aceptación finales aprobadas, capacitación hecha, respaldos restaurados y manuales entregados | 30 % = 6 000 Bs |

<!-- s2 -->
## Alcance

El alcance comprende ocho módulos y las funciones transversales que se listan a continuación. Cada módulo se entrega en la semana indicada.

| Módulo | Qué incluye | Semana de entrega |
|---|---|---|
| 1. Fórmulas por ítem | Ítems, materias primas y unidades; fórmula con líneas, base de cálculo (por unidad, por lote o por kg) y merma global y por línea; versiones con una sola vigente por ítem; aviso si las cantidades no suman lo esperado | 2 y 3 |
| 2. Órdenes de pedido | Pedido de un ítem con cantidad, fecha requerida y prioridad; estados Registrado, Aprobado, En producción, Atendido y Cancelado; numeración correlativa sin duplicados | 3 |
| 3. Órdenes de producción | Emitir una o varias órdenes desde un pedido, con la fórmula congelada al emitir; liberar, cancelar, iniciar, pausar, terminar y cerrar | 3 (emitir, liberar, cancelar) y 5 (resto) |
| 4. Operarios por turno | Operarios, turnos (incluso los que cruzan la medianoche) y asignación por fecha, turno y orden; un operario no puede estar en dos órdenes a la vez | 2 (catálogos) y 4 (asignación) |
| 5. Inventario de materia prima | Ingreso, salida contra orden, ajuste con motivo y devolución; historial de movimientos que no se edita ni se borra; saldo y stock mínimo | 4 |
| 6. Cálculo de materia prima por orden | Cálculo con merma, descuento de lo reservado, semáforo de disponibilidad, lista de faltantes, liberación con faltante solo con permiso y motivo, hoja de orden imprimible y cierre con diferencias básicas | 5 |
| 7. Registro de pesos con balanza | Estación de pesaje con un botón y teclado; captura solo con peso estable; peso neto (bruto menos tara); cantidad estimada de ítems; anulación con motivo; modo manual con permiso | 6 |
| 8. Reportes | Producción por orden, por turno y por período; control de pesos; stock y kardex; visor de auditoría. Con filtros, vista previa, PDF, Excel e impresión | 7 |

**Funciones transversales incluidas:** usuarios con seis roles y permisos; auditoría de cambios con valores antes y después; pruebas automatizadas y de aceptación; instalación del servidor y de los equipos; respaldos; capacitación; 90 días de soporte.

**Se entrega durante el acompañamiento en planta** (11 al 15 de enero de 2027): la pantalla de consumo real contra consumo teórico por orden, con alerta de tolerancia.

**No incluye:**

- Costos y valuación de inventario, compras, facturación, contabilidad, nómina y mantenimiento de máquinas.
- Trazabilidad por lote de proveedor (solo se guarda el dato en el ingreso).
- Pedidos con varios ítems y unión de varios pedidos en una orden.
- Etiquetas o tickets de lote, y hoja de orden con formato preimpreso.
- Acceso web, aplicación móvil y hosting.
- Cola local de pesajes ante caída de la red. Se incluye el mínimo: bloqueo con mensaje claro y conservación del último peso por 5 minutos.
- Pantalla de kiosco, aviso sonoro y teclado en pantalla en la estación de pesaje.
- Reportes de consumo teórico contra real como reporte aparte, reportes programados por correo y gráficos.
- Importación de datos históricos y de fórmulas (las fórmulas se cargan de forma guiada).
- Hardware, servidor, hosting y licencias de terceros.

Estas funciones pueden cotizarse como una segunda fase. Toda lista de exclusiones se firma en el documento de procesos de la semana 1.

<!-- s3 -->
## La solución

El sistema se instala en una PC servidor dentro de la red local de la fábrica, que guarda la base de datos y los respaldos. Las PCs de almacén, producción y gerencia se conectan a ese servidor por cable de red. La estación de pesaje es una PC con la balanza conectada, que lee el peso directamente.

No se necesita internet para operar. Si la empresa solo tiene un PC, todo puede funcionar en él de forma temporal, con mayor riesgo operativo. El acceso desde fuera de la fábrica se resuelve con VPN o escritorio remoto, sin desarrollo. Un visor web de solo lectura se cotiza aparte.

Los respaldos se hacen cada noche a las 23:00, con copia a un segundo disco y conservación de 30 días.

**Equipos que debe proveer la empresa:**

| Equipo | Para qué | Requisito mínimo | Costo estimado (Bs) | Fecha límite |
|---|---|---|--:|---|
| PC servidor | Base de datos y respaldos, encendida 24 horas | Procesador i5, 16 GB de memoria, disco SSD de 480 GB, Windows 11 Pro | 3 000 a 5 500 | Mié 2026-12-02 |
| UPS para el servidor | Evitar daños por cortes eléctricos | 1 000 VA | 600 a 1 000 | Mié 2026-12-02 |
| Switch y cables de red | Conectar los equipos por cable | Switch Gigabit, cable Cat6 | 200 a 450 | Mié 2026-12-02 |
| Disco USB o NAS | Copia de los respaldos | 1 TB | 450 a 700 | Mié 2026-12-02 |
| Acceso de administrador al servidor | Instalar el sistema; IP fija; reglas de antivirus y firewall | Confirmado por el área de sistemas | 0 | Mié 2026-12-02 |
| PCs de trabajo (almacén, producción, gerencia) | Usar el sistema | Windows vigente y conexión a la red; solo si faltan | 2 500 a 4 000 c/u | Lun 2026-12-07 |
| Balanza o indicador con salida de datos | Registro de pesos | Salida serie o de red; si no existe, se decide el modo manual | 0 si existe; 1 500 a 6 000 si se compra | Vie 2026-11-20 (confirmar) |
| Adaptador USB a RS-232 | Conectar la balanza a la PC | Marca confiable (se recomienda FTDI) | 80 a 200 | Vie 2026-12-18 |
| UPS para la PC de pesaje | Proteger la estación | 600 a 1 000 VA | 400 a 800 | Vie 2026-12-18 |
| Hosting, VPS o dominio | Solo si se exige acceso web | No se usa en el plan base | 0 | Decidir en la semana 1 |

La suma de los equipos de red, servidor y estación (sin balanza ni PCs de trabajo) es de 4 730 a 8 650 Bs. Un PC existente puede servir de servidor, pero el riesgo operativo es mayor y debe quedar firmado.

<!-- s4 -->
## Plan por etapas

Cada etapa termina con una demostración y un acta. La aprobación de cada entrega se da en 3 días hábiles; sin respuesta, se entiende aprobada.

### Pre-arranque

**Fechas:** miércoles 2026-11-11 a viernes 2026-11-13.

**Objetivo:** dejar firma, anticipo, accesos y calendario de entrevistas resueltos antes del arranque.

**Entregables:**

- Acta de arranque con alcance, calendario, exclusiones y reglas de trabajo.
- Agenda de la semana 1, plantillas de datos (con el peso unitario por ítem como dato obligatorio) y lista de verificación de la balanza.
- Base técnica del sistema preparada y probada.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Firma de la propuesta y anticipo (la propuesta vence el 2026-12-11) | Antes del lun 2026-11-16 |
| Responsable del proyecto, suplente, usuario clave y administrador de respaldos | Lun 2026-11-16 |
| Calendario de la planta en fin de año (feriados y vacaciones) | Lun 2026-11-16 |
| Fotos del indicador de la balanza y su manual | Lun 2026-11-16 |

**Criterio de aceptación:**

- Firma y anticipo recibidos, y responsable designado por escrito.
- Calendario de entrevistas de la semana 1 confirmado.

**Pago:** hito 1, 30 % = 6 000 Bs.

### Semana 1 – Levantamiento y base de seguridad

**Fechas:** lunes 2026-11-16 a viernes 2026-11-20.

**Objetivo:** validar los procesos de la fábrica con la empresa y decidir el miércoles-jueves cómo se conectará la balanza.

**Entregables:**

- Documento de procesos versión 1 (viernes 2026-11-20): procesos futuros, reglas, matriz de permisos, criterios de aceptación y exclusiones.
- Visita técnica de red y servidor.
- Ficha de la balanza, con tramas reales capturadas y veredicto verde, amarillo o rojo (jueves 2026-11-19).
- Prototipo de pantallas validado con la empresa.
- Usuarios, seis roles, permisos y auditoría funcionando.

**Veredicto de la balanza.** Con verde o amarillo, se usa el lector ya probado. Con rojo (sin salida de datos o protocolo propietario), el modo manual pasa a ser el entregable base del módulo 7. El lector nuevo se cotiza aparte.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Borrador de ítems (con peso unitario), materias primas y al menos 5 ítems representativos con las 3 fórmulas más complejas | Mié 2026-11-18 |
| Operarios y turnos | Jue 2026-11-19 |
| Acceso a la balanza, manual y 2 horas de prueba | Jue 2026-11-19 |
| 3 pedidos y 3 órdenes reales, planilla de turno y movimientos de inventario de 1 mes | Vie 2026-11-20 |
| Respuestas a las preguntas críticas (sección 10) | Vie 2026-11-20 |

**Criterios de aceptación:**

- Las preguntas críticas tienen respuesta escrita o una decisión por defecto firmada.
- Al menos 20 tramas consecutivas de la balanza leídas correctamente y veredicto comunicado el jueves.
- Matriz de permisos probada por rol, con ingreso, bloqueo y cambio de contraseña funcionando.

**Demostración:** viernes 2026-11-20, 15:00.

### Semanas 2 y 3 – Fórmulas, pedidos y órdenes

**Fechas:** lunes 2026-11-23 a viernes 2026-12-04.

**Objetivo:** tener fórmulas versionadas, pedidos y órdenes emitidas desde un pedido, más los catálogos de operarios y turnos, con el servidor de la empresa en marcha.

**Entregables:**

- Módulos 1, 2 y 3 (emitir, liberar y cancelar) en un entorno de pruebas, con datos reales.
- Catálogos de unidades, materias primas, ítems, contenedores, operarios y turnos.
- Documento de procesos firmado.
- Servidor de la empresa con la base de datos instalada y respaldo diario.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Firma del documento de procesos | Mié 2026-11-25 |
| Fórmulas finales firmadas por el jefe de producción | Vie 2026-11-27 |
| Listado final de ítems con peso unitario y tolerancia | Vie 2026-11-27 |
| Confirmación de compra del servidor y UPS; respuestas a las preguntas de reglas (sección 10) | Vie 2026-11-27 |
| Servidor, UPS, switch y disco de respaldo en sitio, con acceso de administrador | Mié 2026-12-02 |

**Criterios de aceptación:**

- Una versión de fórmula vigente no se edita, y activar una nueva archiva la anterior. Una orden conserva la versión con que se emitió.
- Pedido registrado en menos de 1 minuto y orden generada en 3 clics o menos. Numeración sin duplicados con 5 usuarios simultáneos.
- Tres fórmulas reales calculadas coinciden con el Excel del jefe de producción (diferencia de 0,1 kg o menos).

**Demostraciones:** viernes 2026-11-27 (catálogos y fórmula con versiones) y viernes 2026-12-04 (de pedido a orden, con roles).

### Semana 4 – Primera entrega

**Fechas:** lunes 2026-12-07 a viernes 2026-12-11.

**Objetivo:** entregar el primer sistema utilizable, con los módulos 1 a 5, usuarios y permisos, instalado en el servidor de la empresa o, si este se retrasa, en cualquier PC aceptable.

**Entregables:**

- Módulos 4 (asignación de operarios) y 5 (inventario) terminados.
- Sistema instalado en el servidor (o PC aceptable) y en al menos 2 PCs de trabajo, con respaldo nocturno.
- Datos maestros reales cargados: ítems, materias primas, fórmulas, operarios, turnos y stock provisional.
- Acta de entrega parcial.

**Uso de esta etapa y hasta la semana 7:** el sistema es un piloto, con datos maestros reales y movimientos de prueba. El uso real comienza el 2027-01-04. El 2027-01-02 se reinicia la parte de movimientos, y el acta de la semana 4 lo declara. Si la empresa exige uso real desde esta semana, el stock definitivo se carga con ajustes documentados en lugar del reinicio.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Stock inicial provisional (conteo de las materias primas principales) | Vie 2026-12-04 |
| PCs de trabajo disponibles para instalar | Lun 2026-12-07 |
| Usuario clave y 2 personas para las pruebas (4 horas) | Jue 2026-12-10 |
| Aprobación del acta | Mié 2026-12-16 (tácita) |

**Criterios de aceptación:**

- Los 13 casos críticos de la prueba de aceptación en OK, y al menos 90 % de los 21 casos aplicables en OK.
- Sin defectos críticos ni altos abiertos.
- Stock igual a ingresos menos salidas, con diferencia 0 en 10 movimientos de prueba; sin stock negativo; 10 usuarios simultáneos durante 30 minutos sin errores.
- Respaldo restaurable en otra PC.

**Demostración:** viernes 2026-12-11, 2 horas: fórmula, pedido, orden, asignación, ingreso y salida de materia prima, y respaldo.

**Pago:** hito 2, 40 % = 8 000 Bs. La falta del servidor de la empresa no bloquea este hito.

### Semana 5 – Cálculo de materia prima

**Fechas:** lunes 2026-12-14 a viernes 2026-12-18.

**Objetivo:** calcular, reservar y verificar la materia prima de cada orden, con cierre de orden.

**Entregables:**

- Módulo 6 completo: cálculo con merma, reservas, semáforo, faltantes y hoja de orden imprimible.
- Orden con transiciones: iniciar, pausar, terminar y cerrar con diferencias.
- Actualización instalada en horario acordado (7:00 o 13:00), con respaldo previo.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Decisión: una orden con faltante se bloquea o se libera con autorización | Vie 2026-11-27 |
| 5 órdenes reales con su cálculo manual | Mar 2026-12-15 |
| Jefe de producción disponible para validar cálculos (2 horas) | Mié 2026-12-16 |
| Adaptador USB a RS-232 y UPS de la estación de pesaje | Vie 2026-12-18 |

**Criterios de aceptación:**

- En 5 órdenes reales, la necesidad de cada materia prima coincide con el cálculo manual (diferencia de 0,1 kg o menos).
- Con 20 liberaciones simultáneas, lo reservado nunca supera la existencia ni hay stock negativo.
- Liberar con faltante exige permiso y motivo. Cerrar con desviación mayor a 5 % exige motivo.

**Demostración:** viernes 2026-12-18.

### Semana 6 – Balanza y registro de pesos

**Fechas:** lunes 2026-12-21 a jueves 2026-12-24 (4 días hábiles; el 25 de diciembre es feriado).

**Objetivo:** pesar producción con la balanza real y relacionar el peso con la cantidad de ítems.

**Entregables:**

- Módulo 7: configuración de balanza por estación, lectura en vivo, captura con peso estable, retorno a cero, tara por contenedor y registro de pesaje.
- Modo manual con permiso y marca MANUAL; simulador solo para el administrador.
- Prueba en sitio con peso patrón y operador real; acta de la jornada.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Balanza disponible en la estación (jornada de unas 4 horas) | Mar 2026-12-22 |
| Peso patrón conocido y 30 piezas de un ítem para calibrar el peso unitario | Mar 2026-12-22 |
| Un operador y un supervisor para la prueba (2 horas) | Mar y mié 2026-12-22 y 23 |

**Criterios de aceptación:**

- 100 % de tramas válidas en 10 minutos y 30 minutos sin reconexiones espontáneas.
- Error contra peso patrón de una división o menos; 50 pesadas sin pérdida.
- Un peso inestable no se puede capturar. La desconexión se avisa en menos de 5 segundos.
- Un operador pesa un lote en 10 segundos o menos.

**Demostración:** miércoles 2026-12-23 (adelantada por el feriado).

### Semana 7 – Reportes

**Fechas:** lunes 2026-12-28 a jueves 2026-12-31 (4 días hábiles; el 1 de enero es feriado).

**Objetivo:** entregar los reportes con filtros, PDF y Excel, y ensayar con datos reales en los días en que la planta opere.

**Entregables:**

- Reportes de producción por orden, por turno y por período; control de pesos; stock y kardex; y visor de auditoría.
- Bitácora del ensayo con lista priorizada de defectos.
- Materiales de capacitación en borrador.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Calendario real de producción de fin de año | Vie 2026-11-20 |
| Reportes de uso diario, muestra de formatos y tamaño de papel | Vie 2026-12-11 |
| Usuario clave 30 minutos diarios en los días con operación | Lun 2026-12-28 a jue 2026-12-31 |

**Criterios de aceptación:**

- Los totales de cada reporte coinciden al 100 % con las pantallas.
- Un mes con 5 000 pesadas se exporta a PDF y Excel en menos de 30 segundos.
- Sin defectos críticos abiertos tras el ensayo.

**Demostración:** jueves 2026-12-31, o el último día hábil con operación.

### Semana 8 – Entrega final

**Fechas:** lunes 2027-01-04 a viernes 2027-01-08.

**Objetivo:** dejar el sistema instalado, aceptado, con el personal capacitado y los respaldos verificados.

**Entregables:**

- Sistema en producción como registro oficial desde el lunes 2027-01-04, con el papel solo como respaldo.
- Instalación final del servidor y los equipos, con respaldos restaurados en otro PC.
- Stock inicial firmado, tras la toma de inventario del sábado 2027-01-02.
- Capacitación por rol, manuales, tarjetas de uso y plan de contingencia.
- Informe de pruebas y acta final.

**La empresa entrega:**

| Insumo | Fecha límite |
|---|---|
| Lista de usuarios, roles y asistentes a la capacitación | Vie 2026-12-18 |
| Planta detenida y 2 personas (almacén y producción) para el conteo | Sáb 2027-01-02 |
| Administrador de respaldos presente en la prueba de restauración | Mié 2027-01-06 |
| Gerencia disponible para la sesión de reportes | Jue 2027-01-07 |
| Aprobación del acta final | Mié 2027-01-13 (tácita) |

**Criterios de aceptación:**

- Pruebas de aceptación finales: 100 % de los casos críticos en OK, al menos 95 % del total en OK, sin defectos críticos ni altos.
- Un técnico de la empresa instala el servidor y 2 PCs siguiendo el manual en 2 horas o menos.
- Respaldo restaurado en otro PC con los mismos saldos; respaldo nocturno 3 noches seguidas.
- Capacitación con asistencia firmada y evaluación práctica aprobada.

**Demostración y cierre:** viernes 2027-01-08, 2 horas con el responsable y gerencia.

**Pago:** hito 3, 30 % = 6 000 Bs.

**Acompañamiento en producción (11 al 15 de enero de 2027):** dentro del soporte de 90 días, el equipo acompaña la primera semana completa de uso real, corrige lo que aparezca y entrega la pantalla de consumo real contra teórico. Su cierre no retiene el pago final.

<!-- s5 -->
## Cronograma

Las etapas y los hitos se leen en orden de fechas; el 25 de diciembre y el 1 de enero son feriados sin trabajo.

- 2026-11-11 | 2026-11-13 | Pre-arranque | etapa
- 2026-11-16 | 2026-11-16 | Arranque y pago 1 (30 %) | hito
- 2026-11-16 | 2026-11-20 | Semana 1: levantamiento | etapa
- 2026-11-19 | 2026-11-19 | Veredicto de la balanza | hito
- 2026-11-23 | 2026-12-04 | Semanas 2 y 3: fórmulas | etapa
- 2026-11-25 | 2026-11-25 | Firma de procesos | hito
- 2026-12-02 | 2026-12-02 | Servidor y red en sitio | hito
- 2026-12-07 | 2026-12-11 | Semana 4: primera entrega | etapa
- 2026-12-11 | 2026-12-11 | Pago 2 (40 %) | hito
- 2026-12-14 | 2026-12-18 | Semana 5: materia prima | etapa
- 2026-12-21 | 2026-12-24 | Semana 6: balanza | etapa
- 2026-12-25 | 2026-12-25 | Navidad | feriado
- 2026-12-28 | 2026-12-31 | Semana 7: reportes | etapa
- 2027-01-01 | 2027-01-01 | Año Nuevo | feriado
- 2027-01-02 | 2027-01-02 | Toma de inventario | hito
- 2027-01-04 | 2027-01-04 | Arranque oficial | hito
- 2027-01-04 | 2027-01-08 | Semana 8: entrega final | etapa
- 2027-01-08 | 2027-01-08 | Pago 3 (30 %) y acta | hito
- 2027-01-11 | 2027-01-15 | Acompañamiento en planta | etapa
- 2027-01-08 | 2027-04-08 | Soporte de 90 días | etapa

<!-- s6 -->
## Cómo trabajaremos juntos

El proyecto avanza con un responsable por cada parte, reuniones fijas y decisiones por escrito. Lo que no esté firmado no se asume.

| Rol | De quién | Responsabilidad |
|---|---|---|
| Responsable del proyecto y suplente | Empresa | Decide, aprueba entregas, firma actas, convoca al personal y responde en 24 horas |
| Usuario clave | Empresa | Participa en las pruebas, atiende el primer nivel de soporte y valida los cálculos; al menos 4 horas semanales de la semana 4 a la 8 |
| Jefe de producción | Empresa | Fórmulas, pedidos, órdenes, turnos y operarios |
| Almacenero | Empresa | Maestro de materias primas, stock inicial y conteo de arranque |
| Supervisor de turno | Empresa | Operación de órdenes y pesaje, de la semana 6 a la 8 |
| Encargado de balanza o calidad | Empresa | Acceso a la balanza, manual y peso patrón |
| Gerencia | Empresa | Reportes, aprobación de pedidos y fórmulas, y pagos |
| Administrador de respaldos y sistemas | Empresa | Servidor, red, custodia y verificación de respaldos, desde la semana 3 |
| Equipo de desarrollo | Proveedor | Levantamiento, desarrollo, pruebas, instalación, capacitación y soporte |

**Reuniones:**

| Reunión | Cuándo | Duración | Resultado |
|---|---|--:|---|
| Arranque | Lun 2026-11-16 | 1,5 h | Acta de arranque |
| Demostración semanal | Viernes 15:00, desde la semana 1 | 45 min | Observaciones y acta corta |
| Seguimiento | Martes, por llamada o mensaje | 15 min | Bloqueos de la empresa |
| Entrega parcial | Vie 2026-12-11 | 2 h | Acta parcial |
| Entrega final | Vie 2027-01-08 | 2 h | Acta final |

**Aprobación en 3 días hábiles.** Cada entrega se envía con una guía y una lista de verificación. La empresa responde Aprobado, Aprobado con observaciones (lista numerada) o Rechazado (con motivo). Sin respuesta, se envía un recordatorio y, al cuarto día hábil, la entrega se da por aprobada. Esto rige también para las actas semanales y se confirma por escrito en el arranque.

**Control de cambios.** Toda solicitud se clasifica en una de tres categorías. Una corrección es lo que no cumple el documento validado, y no tiene costo. Una aclaración es un detalle menor no definido, y se resuelve en la demostración. Un cambio es una función nueva o la modificación de un proceso validado.

1. La empresa pide el cambio por escrito.
2. El equipo lo clasifica en 1 día hábil.
3. Si es un cambio, entrega en 2 días hábiles su impacto en plazo y costo.
4. El responsable decide por escrito: aprobar, posponer a una segunda fase o rechazar.
5. Se ejecuta solo con el impacto aprobado por escrito. El plazo se amplía en la misma medida.

Los cambios se cotizan aparte. No se ejecuta ninguno sin esa aprobación.

**Actas.** Hay cuatro: arranque, entrega parcial (hito 2), entrega final (hito 3) y cierre de soporte. Cada acta se envía el mismo día. Los defectos menores se listan con fecha de corrección y no retienen el pago.

<!-- s7 -->
## Pruebas, capacitación y puesta en marcha

Cada entrega pasa por pruebas automatizadas del equipo y por pruebas de aceptación con personal de la empresa. El equipo observa y no guía al usuario durante las pruebas de aceptación.

**Pruebas de aceptación:**

| Entrega | Qué se prueba | Condición de aprobación |
|---|---|---|
| Primera entrega (semana 4) | 22 casos con datos reales, 21 aplicables; 13 críticos | 100 % de los críticos en OK; al menos 90 % de los aplicables; sin defectos críticos ni altos |
| Cálculo de materia prima (semana 5) | Casos de cálculo, faltante, reserva cruzada, recálculo y merma | Coincidencia con el cálculo manual del jefe de producción |
| Balanza (semana 6) | Peso patrón, 50 pesadas, desconexión y modo manual | Error de una división o menos |
| Entrega final (semana 8) | 20 casos más repetición de 5 casos críticos de la primera entrega | 100 % de los críticos; al menos 95 % del total; sin defectos críticos ni altos |

**Carga de datos e inventario inicial.** Los ítems y las materias primas se cargan desde plantillas de Excel con encabezados exactos, con una prueba previa y un informe. Las fórmulas las carga el jefe de producción con una guía de una página. El stock provisional (4 de diciembre) sirve solo para el piloto.

El sábado 2027-01-02 se toma el inventario definitivo con la planta detenida:

1. Se congelan los movimientos desde las 18:00 del día anterior.
2. Dos personas cuentan cada materia prima. Se vuelve a contar si la diferencia supera 1 % o 2 kg, y se pesan los sacos abiertos.
3. Se guarda un respaldo, se reinicia la parte de movimientos de prueba y se registra un único movimiento de "Saldo inicial".
4. El usuario clave firma el stock inicial.

**Capacitación por rol:**

| Rol | Contenido | Duración | Cuándo |
|---|---|--:|---|
| Almacenero | Ingreso, salida con stock insuficiente, anulación de movimientos, consulta de stock | 2 h | Sáb 2027-01-02, tras el conteo |
| Usuario clave y administrador de respaldos | Restaurar un respaldo, crear y desbloquear usuarios, reconectar la balanza | 2 h | Lun 2027-01-04, 7:00 |
| Operadores de balanza y supervisores | Iniciar sesión, elegir orden, pesar 3 lotes, reconocer "sin conexión" | 2 h | Lun 2027-01-04, 7:00, en la estación |
| Gerencia, jefe de producción y supervisores | Crear fórmula, pasar de pedido a orden, ver faltantes, cerrar con diferencias, abrir y exportar reportes | 3 h | Jue 2027-01-07 |

Son 9 horas en total. Cada sesión termina con una evaluación práctica. Se aprueba con el 100 % de las tareas clave. Quien no apruebe repite la práctica en el puesto durante la primera semana. Se entrega además una tarjeta plastificada para la balanza, guías de una página por rol y una guía del usuario clave.

**Día de arranque.** El lunes 2027-01-04 a las 7:00 se dan las sesiones del usuario clave y de los operarios. A las 9:00 el sistema pasa a ser el registro oficial, y el papel queda solo como respaldo. Durante las primeras 5 jornadas, la empresa y el equipo concilian cada día, durante unos 10 minutos, el sistema contra el método anterior. El martes y el miércoles se ejecutan las pruebas de aceptación finales.

Si la planta tiene cierre de fin de año, la conciliación se corre a los primeros días con operación y se deja por escrito. El acompañamiento termina el 2027-01-15, o en la fecha acordada, cuando el usuario clave y el supervisor declaran por escrito que pueden trabajar sin el papel en al menos 2 de 3 flujos.

<!-- s8 -->
## Soporte de 90 días

El soporte corre del viernes 2027-01-08 al jueves 2027-04-08 e incluye acompañamiento en planta, corrección de errores y refuerzo de capacitación.

**Qué cubre:**

- Corrección de todo lo que no cumpla el documento de procesos o falle en un caso de prueba aceptado: cálculos, permisos, pantallas y balanza validada.
- Reconfiguración por cambio de puerto COM, reinstalación y recuperación desde respaldo.
- Refuerzo de capacitación para cada rol.
- Dos visitas en el primer mes (semanas 1 y 3) y luego bajo pedido.
- Revisión de incidencias a los 30 días y prueba de restauración a los 60 días.

**Canales y horario:** WhatsApp o teléfono, de lunes a viernes de 8:00 a 18:00; sábados de 8:00 a 12:00 solo para incidencias críticas. Las incidencias se registran en una hoja compartida. No hay guardia nocturna. El acceso remoto se hace solo con autorización de la empresa.

| Severidad | Definición | Primera respuesta | Meta de solución |
|---|---|---|---|
| Crítica | Sistema inutilizable o pérdida de datos o saldos | 4 horas hábiles | 8 horas hábiles |
| Alta | Una función principal falla, pero hay alternativa temporal | 4 horas hábiles | 2 días hábiles |
| Media | Error que no bloquea el trabajo | 1 día hábil | 5 días hábiles |
| Baja | Detalle estético o consulta | 2 días hábiles | Versión de cierre |

**Qué no cubre:** campos, reportes o módulos nuevos; cambios en procesos validados; otra balanza u otro protocolo; visor web; integraciones con otros sistemas; daños por mala operación o por falta de respaldo; fallas del hardware de la empresa. Estos casos se cotizan aparte.

Los cambios que la empresa pida durante el soporte siguen el control de cambios de la sección 6.

**Cierre:** el jueves 2027-04-08 se entrega la versión estable con su registro de cambios, los manuales finales, un respaldo final verificado, el informe de soporte y el acta de cierre con traspaso de contraseñas y custodia de respaldos. El carnaval (8 y 9 de febrero de 2027, aproximado) reduce la disponibilidad; el equipo avisará con anticipación. Después del cierre se puede acordar un soporte mensual continuo.

<!-- s9 -->
## Riesgos y cómo los manejamos

Estos son los riesgos que dependen de la empresa o que le afectan de forma directa. Cada demora de la empresa amplía el plazo en la misma medida.

| Riesgo | Efecto | Cómo lo manejamos | Quién |
|---|---|---|---|
| Datos maestros y fórmulas tardíos, incompletos o sin peso unitario | Se bloquean fórmulas, órdenes, cálculo de materia prima y pruebas | Plantillas con ejemplos desde el lunes 2026-11-16; fechas límite en cada acta; revisión de cada fórmula con el jefe de producción; las variantes de color o medida se tratan como ítems distintos | Responsable de la empresa |
| Balanza sin salida de datos o con protocolo propietario | El módulo 7 pierde la lectura automática | Captura de tramas y veredicto el 2026-11-19; si es rojo, el modo manual es el entregable base y el lector nuevo se cotiza aparte | Equipo de desarrollo y encargado de balanza |
| Fin de año: feriados del 25 de diciembre y 1 de enero, planta cerrada | Semanas 6 y 7 con 4 días hábiles; ensayo con pocos días de producción | Calendario de planta pedido en la semana 1; la conciliación pasa al acompañamiento en producción; la fecha se ajusta a los primeros días con operación | Equipo y empresa |
| Servidor, UPS, PCs o acceso de administrador no llegan a tiempo | Se retrasa la instalación y aumenta el riesgo operativo | Fecha límite 2026-12-02; instalación temporal en un PC aceptable que no bloquea el hito 2; el equipo es costo de la empresa | Responsable y área de sistemas |
| Cambios de proceso tras firmar el documento de la semana 1, o funciones fuera del alcance | Ampliación de plazo y costo | Exclusiones firmadas; control de cambios con aprobación por escrito antes de ejecutar | Equipo y responsable |
| Inventario inicial incorrecto | Saldos erróneos desde el primer día | Doble conteo, sacos abiertos pesados, firma del usuario clave y conteo aleatorio de 5 materias primas los días +1 y +5 | Almacenero |
| Resistencia al cambio; operarios siguen con el papel | El sistema no refleja la producción real | Estación de un solo botón, tarjeta junto a la balanza, capacitación en el puesto y apoyo visible de gerencia | Gerencia |
| Corte eléctrico o respaldos sin responsable | Pérdida de datos | UPS en servidor y estación; copia a un segundo disco; administrador de respaldos nombrado en el acta; alerta si el último respaldo supera 36 horas | Área de sistemas |

<!-- s10 -->
## Preguntas para la reunión de arranque

Estas preguntas se responden en la reunión del 2026-11-16 o hasta la fecha indicada. Si no hay respuesta a tiempo, el plan sigue con la opción de la última columna.

| Pregunta | Por qué importa | Fecha límite | Si no hay respuesta asumimos |
|---|---|---|---|
| ¿Quién es el responsable con autoridad, su suplente, el usuario clave y el administrador de respaldos? | Sin ellos no hay quien apruebe ni quien opere | 2026-11-16 | El gerente es el responsable; sin administrador de respaldos no se instala la semana 4 |
| ¿Acepta la aprobación en 3 días hábiles, la cotización de cambios, las exclusiones y que el plazo se amplíe si hay demoras? | Fija las reglas de trabajo | 2026-11-16 | Se firman tal como están en el acta de arranque |
| ¿Cuántas PCs y estaciones de pesaje hay, y hay un PC que pueda ser servidor encendido siempre? | Define la instalación y los costos | 2026-11-17 | 4 PCs de trabajo, 1 estación y servidor por comprar |
| ¿Cómo se define la fórmula (por unidad, por 100 kg, por lote o por kg)? ¿La merma es sobre el peso neto o bruto? ¿Hay reciclado? ¿Se pesa producto terminado, por lote, con tara de contenedor? | Determina el cálculo de materia prima y el pesaje | 2026-11-18 | Base por millar o por kg, merma sobre el neto, reciclado como una materia prima más, producto terminado por lote con tara |
| ¿Conocen el peso unitario estándar de cada ítem? | Sin él no hay cantidad estimada ni alerta de tolerancia | 2026-11-18 | Se calibra con 30 piezas en la jornada de balanza |
| ¿Cuál es la marca, modelo, puertos y manual de la balanza? ¿Hay acceso el jueves? | Define cómo se lee el peso | 2026-11-19 | Salida serie continua a 9600 baudios; sin acceso el jueves, el veredicto se corre |
| ¿Cuántos turnos hay, con qué horarios, y puede un operario estar en dos órdenes a la vez? | Define la asignación de operarios | 2026-11-19 | 2 turnos; un operario en una sola orden activa por turno |
| ¿Cómo opera la planta en Navidad y Año Nuevo? | Define el ensayo y la conciliación | 2026-11-20 | La planta opera del 28 al 31 de diciembre |
| ¿Un pedido tiene varios ítems? ¿Puede haber órdenes sin pedido? | Define el modelo de pedidos y órdenes | 2026-11-20 | Un ítem por pedido, que puede generar varias órdenes; una urgencia es un pedido urgente |
| ¿Alguien necesita ver reportes fuera de la fábrica? | Define si se cotiza acceso remoto o visor web | 2026-11-20 | Solo red local; VPN opcional sin desarrollo |
| ¿De la semana 4 a la 7 el sistema se usa en producción real o es piloto con reinicio de movimientos? | Define cómo se carga el stock definitivo | 2026-11-20 | Piloto con reinicio el 2027-01-02 |
| Una orden con faltante de materia prima, ¿se bloquea o se libera con autorización? ¿Quién autoriza? | Define la regla de liberación | 2026-11-27 | Se libera con permiso del jefe de producción o gerencia y con motivo |
| ¿Hay acceso de administrador al PC servidor, IP fija y reglas de antivirus y firewall confirmadas? | Permite instalar el servidor | 2026-12-02 | Se instala en un PC aceptable de forma temporal |
| ¿Qué reportes se usarán a diario, y con qué papel y logo? | Define los formatos de la semana 7 | 2026-12-11 | Los cinco comprometidos, en papel carta, con el logo recibido en la semana 1 |
