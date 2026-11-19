# Registro de cambios

Todos los cambios relevantes de este proyecto se documentan en este archivo.
El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es [SemVer](https://semver.org/lang/es/) (`MAYOR.MENOR.PARCHE`): PARCHE = correcciones sin migración; MENOR = funciones nuevas o migraciones; MAYOR = cambios incompatibles con backups anteriores.

## [Sin publicar] - Logo del producto (C con pesa)

Identidad visual del producto, común a todas las entregas. No toca el logo de la empresa cliente (se sigue cargando en Parámetros y es el que ven login, inicio, boleta y reportes).

### Agregado
- **Ícono del programa y del instalador:** `app.ico` (16 a 256 px) en el `.exe`, en las ventanas (`FormBase`), en el instalador y su desinstalador, más la imagen pequeña del asistente.
- **«Ayuda › Acerca de»** muestra el logo del producto junto al nombre.
- **Recursos de marca** en `src/PesajeFaeneo.WinForms/Recursos/`: versión solo ícono y horizontal con el nombre «CAPATAZ», en SVG y PNG. Se regeneran con `python scripts/generar-marca.py`.

### Cambiado
- **El producto pasa a llamarse «Capataz»** (antes «Sistema de Pesaje para Faeneo»): títulos de ventana, login e inicio (cuando la empresa no configuró su nombre), «Acerca de», pie de la boleta, activación, instalador (`Capataz-Setup-<version>-x64.exe`), `Product` del ejecutable, README, guías, manuales (fuentes) y checklist UAT.
- Sin cambios internos: namespaces, proyectos, `AppId`, carpeta de datos `PesajeFaeneo`, mutex, clave de registro y producto de licencia `pesajefaeneo` se conservan, para que las instalaciones existentes se actualicen en el mismo lugar y conserven base y licencia.
- Los `.docx`/`.pdf` de los manuales de cliente no se regeneraron: hay que correr `construir-manuales.py` (y, para las capturas, `capturas-manual.ps1`).

## [1.3.1] - 2026-11-17 - Boleta con el logo de la empresa como marca de agua

Build para Matadero San Ramon. Sin migraciones de la base local.

### Agregado
- **Logo como marca de agua en la boleta de media carta:** el logo cargado en Parámetros se dibuja atenuado (13 %) detrás del contenido; las leyendas ANULADA, MUESTRA y SIMULADO siguen legibles encima. No se aplica al ticket de 80 mm ni a los reportes. El perfil es fijo en el código (`DisenoBoleta.LogoComoFondo`, `DisenoBoleta.OpacidadFondoLogo`).

## [1.3.0] - 2026-10-13 - Licencias: clave de activación y prueba de 15 días (F13)

Medida antipiratería, igual que en ScaleLogger. El sistema se activa con una clave de un solo equipo o se usa en prueba durante 15 días. Usa el mismo servidor de licencias y la misma base que ScaleLogger, con el producto `pesajefaeneo` (plan `docs/plans/F13-licencias-activacion.md`). No hay migraciones de la base local.

### Agregado
- **Pantalla «Activar Pesaje Faeneo»** antes del inicio de sesión cuando el equipo no está activado:
  - Con el sistema sin activar ofrece **Activar**, **Probar 15 días** y **Salir**.
  - Con la prueba vencida ofrece **Activar**, **Continuar en modo consulta** y **Salir**.
  - Muestra el **Código de equipo**, que soporte usa para ubicar el equipo en el servidor.
- **Clave de activación** con formato `XXXX-XXXX-XXXX-XXXX-XXXX`, válida para un solo equipo. La primera activación necesita internet; después el sistema funciona sin conexión. Reinstalar en el mismo equipo no consume otra clave.
- **Prueba de 15 días:**
  - Se registra en el servidor; si se borran los datos locales, se recupera la fecha original.
  - Vence si se atrasa el reloj de Windows más de un día o si se alteran los datos de la prueba.
- **Modo consulta** (prueba vencida o clave revocada):
  - Se puede entrar, consultar el historial y los reportes, reimprimir, editar y anular boletas, y hacer copias de seguridad.
  - No se puede **pesar** ni **importar datos legados**. El servicio también lo rechaza (`LIC_MODO_CONSULTA`).
- **Barra de estado:** muestra «Prueba: N días» (naranja) o «Modo consulta» (rojo); con un clic se abre la activación. Si el sistema está activado no muestra nada.
- **Ayuda › Activar licencia…**, visible mientras el sistema no esté activado. **Ayuda › Acerca de…** muestra la licencia y el código de equipo.
- **Sincronización con el servidor** una vez por arranque, en segundo plano: detecta revocaciones y restaura la activación o la fecha de la prueba.

### Servidor de licencias (Supabase, compartido con ScaleLogger)
- Migración `supabase/migrations/20261013000000_licencias_por_producto.sql`. Agrega la columna `product` a `license_keys` y `trials` y las funciones `activate_product_license`, `register_product_trial` y `check_product_license`.
- Para administración agrega `generate_product_license_keys`, `revoke_license_key` y `release_license_key`.
- Las funciones de ScaleLogger 0.1.0 conservan firma y respuesta, así que ScaleLogger no se recompila.

### Documentación
- README (sección «Licencias y activación»), guía de instalación y soporte (activación, actualización a 1.3.0 y administración de claves en la sección 16), manual técnico, manuales de cliente 1.3.0 (nuevo capítulo «Licencia del sistema» en el del Administrador y aviso de modo consulta en los del Operador y el Supervisor) y checklist UAT (UAT-F13-01 a 08).

## [1.2.1] - 2026-10-13 - Correcciones de la revisión de manuales (F12)

Defectos de la aplicación detectados al revisar los manuales contra el código (plan `docs/plans/F12-correcciones-revision-manuales.md`). Sin migraciones.

### Corregido
- **Fletero escrito sin elegir (pesaje y edición de boleta):** si el texto del fletero no quedó elegido de la lista (varias coincidencias o ninguna), ya no se graba la boleta sin fletero en silencio. En el registro de pesaje la guía pide «Elija el fletero de la lista o borre el texto.» y **Grabar (F12)** queda deshabilitado; en Editar boleta, **Guardar (F12)** marca el campo con ese mensaje. Con el campo vacío se sigue grabando sin fletero.
- **Grabar (F12) deshabilitado** se veía verde: ahora se ve gris mientras no se puede grabar.
- **Pantalla de inicio:** la línea de atajos muestra solo las teclas que el usuario puede usar (el Operador ya no ve «F6 Buscar boletas»).
- **Guía del pesaje con la balanza vacía:** con el peso en reposo (hasta 2,0 kg) ya no dice «Peso fuera de rango (10,0 a 600,0 kg).», sino qué lado capturar. En media res con los dos lados capturados, el aviso «… Use F8 para borrar el pesaje.» aparece aunque la res siga en la balanza.
- **Anular boleta:** el motivo (5 a 100 caracteres) se valida **antes** de la pregunta «¿Desea continuar?».
- **Prueba de conexión de la balanza:** si conecta pero no llega ninguna trama, ya no informa «Conexión correcta: 0 de 0 tramas…», sino «Se conectó, pero no llegó ninguna trama…» con la pista de revisar la «Separación de tramas».

### Documentación
- Revisión completa de los manuales de cliente, el manual técnico y las guías de balanza e instalación contra el código (textos de pantalla, reglas, permisos, capturas y anotaciones); manuales actualizados a 1.2.1 con el comportamiento corregido y capturas regeneradas.

## [1.2.0] - 2026-10-08 - Tipo de pesaje configurable (F11)

Se elige en la configuración si la empresa pesa **res completa y media res**, **solo res completa** o **solo media res**; las empresas que pesan un solo tipo dejan de ver lo relativo al otro. Incluye la migración `F11_ModoDesposte`, por lo que la versión pasa a `1.2.0`.

### Agregado
- **Tipo de pesaje** (**Administración › Tipo de pesaje**, solo Administrador): `FrmTipoPesaje` con las tres opciones, aviso cuando hay boletas del tipo que se deja de pesar y confirmación al cambiar un tipo ya definido. El cambio queda en la auditoría (`CambioParametros`, campo "Tipo de pesaje") y nunca modifica boletas grabadas.
- **Paso obligatorio en el asistente de puesta en marcha:** "3. Tipo de pesaje". Sin elegirlo no se puede finalizar la puesta en marcha (también lo exige el servicio).
- Migración `F11_ModoDesposte`: `ParametrosSistema.ModoDesposte` (opcional, `CHECK` 1..3). Las instalaciones que ya terminaron la puesta en marcha quedan en "Res completa y media res" (sin cambios de comportamiento); las nuevas quedan sin definir hasta elegirlo en el asistente (mientras tanto el sistema funciona como "ambos").

### Cambiado
- **Registro de pesaje:** con un solo tipo, la fila "Desposte" no se muestra y el tipo queda fijo; las reglas de pesaje no cambian (res completa: dos lados con retorno a cero; media res: un lado). Al grabar, el servicio rechaza un tipo que la configuración no permite (`PES_DESPOSTE_NO_PERMITIDO`).
- **Boleta, buscador, historial, edición, anulación y reimpresión:** el desposte se oculta cuando coincide con el único tipo que pesa la empresa. Si en lo mostrado hay boletas de otro tipo (anteriores al cambio o importadas), el desposte se sigue mostrando ("ocultar, nunca falsear"). En la media carta el lugar del desposte queda en blanco para no alterar la calibración del papel.
- **Reportes (grilla, PDF y Excel):** con un solo tipo y sin boletas de otro, se omiten la columna "Tipo Corte", el desglose "res completa / media res" y la fila de Excel equivalente. El Excel arma sus columnas desde una lista (antes por posición fija); con desposte visible el archivo es igual al anterior.
- **Importación:** una boleta legada de un tipo que la configuración no permite se importa igual, con una advertencia en el informe.

### Documentación
- Manuales para clientes 1.2.0 regenerados con capturas nuevas: sección «El tipo de pesaje» en el manual del Administrador (asistente con 8 pasos), notas en los manuales del Operador (sin campo Desposte con un solo tipo) y del Supervisor (reportes sin "Tipo Corte"). Manual técnico, checklist UAT (UAT-F11-01 a 06) y acta actualizados.

## [1.1.0] - 2026-10-05 - Observaciones del cliente (F10) y herramienta de capturas para los manuales

Cambios pedidos por el cliente tras la revisión de F9. Incluye la migración `F10_ObservacionesCliente`, por lo que la versión del producto pasa de `1.0.0` a `1.1.0` (`<Version>` en `Directory.Build.props`).

### Herramientas (no forman parte del producto instalado)
- **Build de capturas para los manuales:** `scripts/capturas-manual.ps1` compila el proyecto WinForms con la constante `CAPTURAS_MANUAL` (`-p:CapturasManual=true`, con bin y obj en una carpeta aparte) y corre un guion que abre las pantallas reales con datos de demostración (empresa ficticia «FRIGORÍFICO EJEMPLO S.R.L.») y las captura a 96 DPI (100 %) en PNG, con un `capturas.json` que lista los controles de cada pantalla. Todo el código nuevo está dentro de `#if CAPTURAS_MANUAL`: ni `scripts/publicar.ps1` ni el instalador definen la constante, así que el ejecutable publicado no cambia. Usa una carpeta de datos propia (`--datos`), nunca `C:\ProgramData\PesajeFaeneo`.

### Documentación
- **Manuales de usuario para clientes:** `docs/manual/clientes/` con tres manuales por rol (Operador, Supervisor y Administrador) en Word y PDF, escritos para personas sin experiencia en aplicaciones de escritorio, con capturas reales de la versión 1.1.0 anotadas con círculos numerados y una hoja de teclas rápidas imprimible. Se regeneran con `python docs\manual\clientes\construir-manuales.py` (ver `docs/manual/clientes/README.md`). `docs/manual/manual-usuario.md` queda como referencia técnica para soporte.

### Cambiado
- **Editar boleta:** **Dirección**, **Color / raza** y **Observación** ya no muestran "(\*)"; son opcionales, igual que al pesar.
- **Número de boleta automático (O1):** el "Siguiente número de boleta" ya no se puede escribir a mano una vez emitida la primera boleta (vigente o anulada); solo lo mueven el grabado y la importación. Mientras no exista ningún pesaje se puede fijar el número inicial para continuar el talonario anterior. El servicio lo exige (`PAR_NUMERACION_AUTOMATICA`) y la pantalla de Parámetros bloquea el campo con una nota.
- **"Borrar pesos" pasa a "Borrar pesaje" (O2):** solo cambia el nombre (botón `F8`, mensajes guía y manual); descarta los pesos del pesaje en curso.
- **Ganadero: "Marca" pasa a "Código" (O4):** en pantallas, boleta, reportes (grilla, PDF y Excel), importador, auditoría, código y base de datos (columna `Ganaderos.Codigo`). Se conservan los códigos alfanuméricos heredados (`AH`); el importador sigue reconociendo columnas llamadas `MARCA`. Los registros de auditoría antiguos con la clave `Marca`/`Orden` se muestran con la etiqueta nueva.
- **Códigos automáticos (O6):** ganaderos, clientes y fleteros reciben un código correlativo al crearse (mayor existente, incluidos inactivos, + 1; reintento ante dos altas simultáneas). El código se muestra en solo lectura y no se puede cambiar. La importación del sistema anterior es el único camino que conserva el código del archivo. Se eliminó la sugerencia de código de cliente y la lógica de "reactivar" al repetir una clave en el alta.
- **Fleteros sin límite y elegidos al pesar (O5):** se quitó el tope de 4 fleteros (F1..F4) y el orden pasa a ser el código automático. El fletero es opcional y se elige en el registro de pesaje (selector tras la dirección); la boleta imprime solo el elegido, es editable al editar la boleta y figura en el buscador, el historial y los reportes (columna Fletero).
- **Boleta, ubicación del fletero (O9):** el fletero va en la zona de datos (media carta: fila de la dirección; ticket: línea "Fletero:") y el recuadro central inferior pasa a ser la **firma del fletero** (con su nombre); desaparecen la lista con casillas y "Otros". El ticket reparte mejor los espacios (alto estimado por línea) y se agregó la firma del fletero.
- **Registro de pesaje sin grilla del día (O3):** los pesos, los botones y el simulador se reubicaron en la columna derecha, junto a los datos. `F9` imprime ahora el último pesaje grabado en la pantalla.

### Agregado
- **Historial de pesajes (O3):** apartado propio (**Pesaje › Historial de pesajes**, `F4` y tarjeta en la pantalla de inicio) con selector de día, anuladas en gris y reimpresión (`F9`) con el flujo de reimpresión existente; visible para todos los roles con permiso de pesar.
- **Logo y nombre de la empresa en el inicio de sesión (O7):** nueva operación de servicio sin sesión (`ObtenerIdentidadEmpresaAsync`, solo nombre y logo); un error al leerlos solo se registra en el log.
- **Reporte "Pesajes por raza" (O8):** nuevo tipo de reporte (`TipoReporte.PorRaza`) con combo de razas existentes, comparación normalizada, solo boletas vigentes y las mismas reglas de totales.
- Migración `F10_ObservacionesCliente`: renombra `Ganaderos.Marca` y `Fleteros.Orden` a `Codigo`, vuelve único el código de fletero entre todos (renumera los inactivos que chocaban) con `CHECK (Codigo >= 1)` y agrega `Pesajes.FleteroId` (opcional, con clave foránea).

### Notas
- Pendiente de validar con el cliente: la ubicación del fletero con una **impresión física** (el riesgo R29 de formato de boleta sigue abierto).
- Los registros de auditoría históricos conservan las claves `Marca` y `Orden` (son inmutables).

## [1.0.0] - sin publicar

### Agregado
- **F0 — Fundaciones del proyecto:** estructura de solución en capas, entidades de dominio, host genérico con DI y Serilog, EF Core con SQLite (WAL), datos iniciales, manejo global de excepciones e infraestructura de pruebas.
- **F1 — Seguridad, usuarios y shell principal:** autenticación con bloqueo por intentos, roles y permisos, pantalla de inicio de sesión, menú principal con autorización, gestión de usuarios y cambio de contraseña.
- **F2 — Parámetros y catálogos:** datos de la empresa, ganaderos, clientes, fleteros (F1..F4) y control reutilizable de búsqueda con autocompletado.
- **F3 — Integración con balanza:** lectores serie (RS-232/USB, kernel32 directo) y TCP, parser de tramas por regex, detector de estabilidad, simulador, servicio compartido y diagnóstico.
- **F4 — Registro de pesaje:** reglas de dominio, numeración atómica de boletas, secuencia por ganadero, pantalla de pesaje con display en vivo, captura, flujo por teclado y alta rápida de catálogos.
- **F5 — Boleta de pesaje:** diseño de boleta (media carta y ticket 80 mm), impresión y vista previa, impresión automática, reimpresión y calibración por equipo.
- **F6 — Edición, anulación y auditoría:** buscador de boletas, edición sin modificar pesos, anulación con motivo, auditoría con diferencias y visor de auditoría.
- **F7 — Reportes:** servicio común de reportes, índices y rendimiento, exportación a PDF (QuestPDF) y Excel (ClosedXML), impresión y reporte de boletas anuladas.
- **F8 — Utilitarios:** backup manual y automático con retención, restauración validada, importador de datos legados (CSV, DBF, Access) y visor de logs.
- **F9 — Empaquetado, instalador y puesta en marcha:**
  - Versión única (`Directory.Build.props`) y "Ayuda › Acerca de".
  - Publicación self-contained por script y autoprueba de la instalación (`--autoprueba`).
  - Migraciones automáticas al iniciar con resguardo previo (`PreMigracion`), detección de base de una versión más nueva y auditoría `ActualizacionSistema`.
  - Instalador Inno Setup por equipo con permisos de datos para usuarios estándar y datos conservados al desinstalar.
  - Instancia única por equipo también entre sesiones de Windows.
  - Asistente de puesta en marcha para el Administrador y recuperación local de la cuenta `admin` (`--restablecer-admin`).
  - Checklist de aceptación en sitio (UAT), acta de aceptación, manual de usuario, guía de balanza y guía de instalación y soporte.

### Notas
- Instalador sin firma de código: Windows SmartScreen o el antivirus pueden advertir al ejecutarlo.
- QuestPDF se usa bajo licencia Community: el cliente debe confirmar que cumple sus condiciones.
- Un PDF de decenas de miles de boletas puede consumir varios GB de memoria: se recomiendan 8 GB de RAM.
- No hay reversión automática de migraciones: ante un fallo, la guía de soporte describe la vuelta atrás desde el resguardo `PesajeFaeneo_premigracion_*.zip`.
- Una sola instancia del sistema por equipo; los datos de `C:\ProgramData\PesajeFaeneo` son legibles por los usuarios locales de Windows.
- Pendiente de validar en sitio: instalador en una PC limpia, conversión de peso con una pesa patrón y formato real de los datos del sistema anterior.
