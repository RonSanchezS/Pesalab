# Checklist de aceptación en sitio (UAT) — Sistema de Pesaje para Faeneo

Este documento se completa a mano durante la puesta en marcha en el sitio del cliente. Al terminar, el resumen se traslada al acta de aceptación (`acta-aceptacion.md`).

## Datos de la prueba

| Dato | Valor |
|---|---|
| Cliente | |
| Sitio | |
| Fecha | |
| Versión instalada | |
| SHA-256 del instalador | |
| Nombre del equipo | |
| Windows (edición y versión) | |
| Indicador de balanza (marca/modelo) | |
| Impresora (marca/modelo) | |
| Responsable del cliente | |
| Responsable del implementador | |

## Preparación

Antes de empezar, tenga disponibles:

- PC de producción (Windows 10/11 x64) donde se instalará el sistema.
- Balanza e indicador conectados a la PC.
- Adaptador USB-serie (si el indicador es RS-232).
- Pesas patrón: al menos una certificada, idealmente de 20 kg o más, y una carga de 100 kg o más si existe.
- Impresora con papel o los formularios del cliente.
- Pendrive vacío.
- Muestra real de datos del sistema anterior (ganaderos, clientes y boletas en CSV, DBF o Access).
- Copia de seguridad del sistema anterior, si se está reemplazando una PC.
- El instalador y su archivo `.sha256`.

## Cómo registrar los resultados

En la columna **Resultado** de cada caso anote:

- `OK`: el caso se cumplió tal como se esperaba.
- `FALLA`: el caso no se cumplió.
- `OBS`: funciona, pero con una observación.

Toda `FALLA` u `OBS` lleva una línea en la columna **Observaciones** y pasa al acta de aceptación.

## Instalación (UAT-INS)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-INS-01 | F9-03 | 1. Use una PC limpia con Windows 10 u 11 x64 sin .NET instalado. <br> 2. Ejecute el instalador de la versión anotada en "Datos de la prueba". <br> 3. Si Windows muestra la advertencia SmartScreen, anótelo en Observaciones y continúe con "Más información › Ejecutar de todas formas". <br> 4. Complete el asistente de instalación, marcando la tarea de acceso directo en el escritorio. <br> 5. Abra el menú Inicio y busque el acceso del sistema. | La instalación termina sin pedir prerrequisitos (ni .NET ni otros componentes). Existe el acceso en el menú Inicio y, si se marcó, en el escritorio. Queda anotado si apareció SmartScreen. | | |
| UAT-INS-02 | F9-03 | 1. Abra PowerShell en la carpeta donde está el instalador. <br> 2. Ejecute `Get-FileHash <instalador>.exe -Algorithm SHA256`. <br> 3. Compare el valor con el contenido del archivo `.sha256` del instalador. <br> 4. Anote el valor en "Datos de la prueba". | El SHA-256 calculado coincide exactamente con el del archivo `.sha256`. | | |
| UAT-INS-03 | F9-03 | 1. Inicie sesión en Windows con un usuario **estándar** (sin permisos de administrador). <br> 2. Abra el sistema desde el menú Inicio. <br> 3. En la pantalla de inicio de sesión ingrese `admin` / `admin`. | El sistema abre sin pedir elevación, crea su base de datos y permite entrar con admin/admin (el siguiente paso es el cambio obligatorio de contraseña, ver UAT-PRI-01). | | |
| UAT-INS-04 | F9-03 | 1. Cierre el sistema en la sesión anterior (**Sesión › Salir**). <br> 2. Cambie de usuario en Windows a **otro** usuario estándar. <br> 3. Abra el sistema e ingrese. <br> 4. Registre una boleta de prueba (**Pesaje › Registrar pesaje**, tecla `F2`) y grábela con `F12`. <br> 5. Abra **Utilitarios › Backup / Restaurar** y pulse "Crear backup ahora". | El segundo usuario puede abrir el sistema, grabar la boleta y crear el backup sin errores de permisos sobre `C:\ProgramData\PesajeFaeneo`. El backup aparece en la lista. | | |
| UAT-INS-05 | F9-03 | 1. Con el sistema abierto en una sesión de Windows, cambie a otra sesión de Windows (otro usuario) sin cerrar la primera. <br> 2. Intente abrir el sistema en la segunda sesión. | Aparece el aviso "El sistema ya está en ejecución en este equipo…" y la segunda instancia termina sin mostrar errores. | | |
| UAT-INS-06 | F9-01 | 1. Cierre el sistema. <br> 2. Cree la carpeta `C:\Temp` si no existe. <br> 3. En PowerShell ejecute: `& "C:\Program Files\PesajeFaeneo\PesajeFaeneo.WinForms.exe" --autoprueba "C:\Temp\autoprueba-uat"`. <br> 4. Abra el archivo `autoprueba.json` generado en esa carpeta. | Se genera `C:\Temp\autoprueba-uat\autoprueba.json` y contiene `"Exito": true`. | | |
| UAT-INS-07 | F9-01 | 1. Ingrese al sistema. <br> 2. Abra el menú **Ayuda › Acerca de…**. <br> 3. Compare la versión mostrada con la anotada en "Datos de la prueba". <br> 4. Pulse "Copiar para soporte" y pegue el contenido en el Bloc de notas. | La ventana muestra la versión instalada. El botón "Copiar para soporte" copia los datos al portapapeles y se pueden pegar en un texto. | | |

## Primer arranque (UAT-PRI)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-PRI-01 | F1/F9-04 | 1. Abra el sistema por primera vez. <br> 2. Ingrese `admin` / `admin`. <br> 3. Intente continuar sin cambiar la contraseña. <br> 4. Ingrese una contraseña nueva (de 6 a 100 caracteres, distinta del usuario y de la anterior) y confírmela. | El sistema obliga a cambiar la contraseña de `admin` antes de entrar al menú principal. Con la nueva contraseña se accede normalmente. | | |
| UAT-PRI-02 | F9-04 | 1. Tras el cambio de contraseña, observe la ventana "Asistente de puesta en marcha". <br> 2. Con empresa y balanza sin configurar, verifique el estado del botón "Finalizar". <br> 3. Configure los datos de la empresa y la balanza desde los botones "Abrir…" del asistente (o confirme el modo simulador con la casilla "Continuar con la balanza en modo SIMULADOR…"). <br> 4. Vuelva a verificar el botón "Finalizar". | El asistente aparece automáticamente. "Finalizar" permanece deshabilitado (el resumen indica "Falta: datos de la empresa." o "Falta: configurar la balanza o confirmar el simulador.") hasta que los dos pasos obligatorios estén completos. | | |
| UAT-PRI-03 | F9-04 | 1. Con el asistente abierto, pulse "Omitir por ahora". <br> 2. Cierre sesión (**Sesión › Cerrar sesión**, `Ctrl+Mayús+L`) e ingrese de nuevo como `admin`. <br> 3. Cree un usuario con rol Operador (**Administración › Usuarios**) y complete su cambio de contraseña inicial. <br> 4. Ingrese como ese Operador. | Al volver a entrar como Administrador el asistente aparece otra vez. Un Operador nunca ve el asistente (ni la opción **Ayuda › Asistente de puesta en marcha…**). | | |
| UAT-PRI-04 | F9-04 | 1. Ingrese como `admin`; en el asistente complete empresa y balanza y pulse "Finalizar". <br> 2. Cierre sesión e ingrese de nuevo como `admin`. <br> 3. Abra **Administración › Visor de auditoría** y busque el registro de la puesta en marcha. | El asistente no vuelve a aparecer al ingresar. El visor de auditoría muestra un registro con el motivo "Puesta en marcha completada". | | |
| UAT-PRI-05 | F9-04/F8 | (Solo si esta PC reemplaza a otra.) <br> 1. Copie a esta PC el archivo `.zip` de backup del equipo anterior. <br> 2. Ingrese como `admin` y abra el asistente (**Ayuda › Asistente de puesta en marcha…**), paso "1. Copias de seguridad". <br> 3. Pulse "Abrir…" (se abre la ventana Backup / Restaurar) y elija "Restaurar desde archivo…". <br> 4. Seleccione el `.zip`, revise el resumen y confirme "Restaurar". <br> 5. Espere el reinicio automático del sistema e ingrese. | La restauración se valida y programa, el sistema se reinicia solo y los datos del equipo anterior (ganaderos, clientes, boletas) están presentes. Si el caso no aplica, anotar "N/A" en Resultado. | | |

## Seguridad (UAT-SEG)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-SEG-01 | F1 | 1. Ingrese como `admin` y abra **Administración › Usuarios**. <br> 2. Cree un usuario Operador y otro Supervisor con "Nuevo". <br> 3. Ingrese con cada uno (cambiarán su contraseña inicial) y revise los menús visibles. <br> 4. Compare con la matriz de roles del manual de usuario (Anexo B). | Cada rol ve solo los menús que le corresponden: el Operador no ve edición/anulación de boletas, reportes, catálogos ni administración; el Supervisor ve reportes, edición/anulación, catálogos y visor de auditoría, pero no usuarios, parámetros, balanza, impresión ni utilitarios. | | |
| UAT-SEG-02 | F1 | 1. Cierre sesión. <br> 2. Con el usuario Operador de prueba, ingrese 5 veces una contraseña incorrecta. <br> 3. Intente ingresar con la contraseña correcta inmediatamente. <br> 4. Espere 5 minutos y vuelva a intentar. | Tras 5 intentos fallidos la cuenta queda bloqueada durante 5 minutos (incluso con la contraseña correcta). Pasado ese tiempo se puede ingresar. El mensaje de error es siempre "Usuario o contraseña incorrectos." | | |
| UAT-SEG-03 | F9-04 | 1. Cierre el sistema. <br> 2. Abra PowerShell **sin** elevar y ejecute `& "C:\Program Files\PesajeFaeneo\PesajeFaeneo.WinForms.exe" --restablecer-admin`; anote el aviso. <br> 3. Abra PowerShell **como administrador de Windows** y ejecute el mismo comando; confirme el mensaje "Restablecer administrador" con "Sí" y anote la contraseña temporal que muestra. <br> 4. Abra el sistema e ingrese como `admin` con la contraseña temporal. <br> 5. Cambie la contraseña cuando el sistema lo exija. <br> 6. Abra **Administración › Visor de auditoría** y busque el registro de la recuperación. | Sin elevar, el comando muestra un aviso indicando que debe ejecutarse como administrador de Windows y no cambia nada. Elevado, entrega una contraseña temporal. El ingreso con ella exige cambiarla y el hecho queda registrado en la auditoría ("Recuperación local de la cuenta de administrador"). | | |

## Balanza (UAT-BAL)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-BAL-01 | F3 | 1. Con el indicador real conectado, abra **Administración › Configuración de balanza**. <br> 2. Configure tipo de conexión, puerto, baudios, separación de tramas y unidad según la guía de balanza (§3). <br> 3. Pulse "Probar conexión" en el diagnóstico en vivo. <br> 4. Anote el estado y el peso mostrados. | La prueba informa tramas válidas, muestra el peso en kg y el estado ESTABLE cuando la carga está quieta. | | |
| UAT-BAL-02 | F3 | 1. Con "Probar conexión" activo, coloque la primera pesa patrón certificada. <br> 2. Espere el estado estable y anote el peso mostrado; repita hasta tener 3 lecturas. <br> 3. Repita para cada pesa patrón disponible (idealmente una de 20 kg o más y una de 100 kg o más). <br> 4. Calcule la diferencia entre cada lectura y el valor certificado. | La diferencia con el valor certificado no supera la resolución del indicador (0,1 kg en el sistema). Los valores certificados y leídos quedan anotados en Observaciones. | | |
| UAT-BAL-03 | F3 | 1. Con el sistema abierto y la balanza en lectura, observe la barra de estado inferior ("Balanza: …"). <br> 2. Desconecte el adaptador USB-serie. <br> 3. Anote el estado que muestra la barra. <br> 4. Reconecte el adaptador y espere. <br> 5. Si no se recupera, revise el número de COM y corríjalo en **Administración › Configuración de balanza** (guía de balanza §2) y pulse "Reconectar". | Al desconectar, la barra de estado indica el problema (sin datos o error de conexión). Al reconectar, la lectura se recupera, o se recupera tras reconfigurar el COM según la guía. | | |
| UAT-BAL-04 | F3/F4 | 1. Abra **Pesaje › Registrar pesaje** (`F2`). <br> 2. Con una carga oscilando (balanceándose o moviéndose) sobre la balanza, observe el indicador de estabilidad. <br> 3. Intente capturar con `F5`. | El peso en vivo figura como INESTABLE y la captura no se realiza mientras la carga oscile. | | |

## Pesaje (UAT-PES)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-PES-01 | F4 | 1. Abra **Pesaje › Registrar pesaje** (`F2`). <br> 2. Seleccione ganadero y cliente, y deje el desposte en Res Completa. <br> 3. Con la carga estable, pulse `F5` para capturar el lado izquierdo. <br> 4. Retire la carga hasta que la balanza vuelva a cero. <br> 5. Coloque el otro lado, espere estable y pulse `F6`. <br> 6. Pulse `F12` para grabar. | La boleta se graba con el número de boleta siguiente al anterior (el panel muestra el mensaje de grabado), el total es la suma de ambos lados y los campos se reinician para la siguiente pesada. | | |
| UAT-PES-02 | F4 | 1. En **Pesaje › Registrar pesaje** elija el desposte Media Res. <br> 2. Capture un solo lado (`F5` o `F6`). <br> 3. Pulse `F12`. | Se graba la boleta con un solo lado y el total es igual al peso de ese lado. | | |
| UAT-PES-03 | F4 | 1. Con desposte Res Completa, capture el lado izquierdo con `F5`. <br> 2. Sin retirar la carga de la balanza, intente capturar el segundo lado con `F6`. <br> 3. Lea el mensaje del panel. | La captura queda bloqueada y el panel muestra un mensaje guía indicando que la balanza debe volver a cero antes de capturar el segundo lado. | | |
| UAT-PES-04 | F4 | 1. En **Pesaje › Registrar pesaje** teclee en "Ganadero" una marca que no exista y pulse `Enter`. <br> 2. Complete el diálogo "Alta rápida de ganadero" y guarde. <br> 3. Repita en "Cliente" con un nombre nuevo (diálogo "Alta rápida de cliente"). | Se abre el diálogo de alta rápida con el texto tecleado prellenado, y al guardar el nuevo ganadero/cliente queda seleccionado en la pantalla de pesaje. | | |
| UAT-PES-05 | F4 | 1. En **Pesaje › Registrar pesaje** seleccione un ganadero y anote la secuencia sugerida. <br> 2. Grabe una boleta de ese ganadero. <br> 3. Seleccione de nuevo el mismo ganadero y anote la secuencia sugerida. <br> 4. Al día siguiente repita el paso 1 (o, si no es posible, compruebe con la fecha de una boleta del día anterior en el buscador de boletas). | La secuencia sugerida aumenta en 1 con cada boleta del ganadero en el mismo día y vuelve a 1 al día siguiente. | | |
| UAT-PES-06 | F4 | 1. Grabe una boleta con `F12` y anote su número. <br> 2. Inmediatamente después abra el Administrador de tareas de Windows y finalice el proceso del sistema (`PesajeFaeneo.WinForms`). <br> 3. Vuelva a abrir el sistema e ingrese. <br> 4. Busque la boleta en **Boletas › Buscar / editar boleta** (`F6`) o en el reporte del día. <br> 5. Grabe una boleta nueva y compare su número. | La boleta grabada antes del corte existe en la base de datos con sus datos completos, y la numeración continúa sin huecos ni duplicados. | | |

## Boleta (UAT-BOL)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-BOL-01 | F5 | 1. Abra **Administración › Configuración de impresión**. <br> 2. Elija la impresora real, el formato de papel y marque "Imprimir la boleta automáticamente al grabar"; guarde. <br> 3. Grabe una boleta de prueba desde **Pesaje › Registrar pesaje**. <br> 4. Compare la boleta impresa con el formato del cliente. | La boleta se imprime automáticamente al grabar y contiene los datos esperados. Se anotan las diferencias de formato (título, formulario preimpreso, ubicación de campos) en Observaciones. | | |
| UAT-BOL-02 | F5 | 1. En **Administración › Configuración de impresión** pulse "Imprimir página de calibración". <br> 2. Mida con regla la distancia del recuadro a cada borde del papel. <br> 3. Si no coincide, ajuste los desplazamientos X e Y (en mm) y repita. | El recuadro impreso queda a 10 mm de cada borde del papel. Si no, se ajustan los desplazamientos hasta lograrlo y se anotan los valores finales. | | |
| UAT-BOL-03 | F5 | 1. Pulse `F3` en la pantalla principal (o **Boletas › Reimprimir boleta**). <br> 2. Con el número de una boleta ya impresa, pulse `Enter` para buscarla. <br> 3. Pulse `F4` para ver la vista previa y luego `F9` para imprimir. | La boleta reimpresa lleva la leyenda "REIMPRESIÓN n" (n = número de impresión) con fecha y hora. | | |
| UAT-BOL-04 | F5 | 1. Apague la impresora (o desconéctela). <br> 2. Grabe una boleta con `F12` y lea el mensaje del panel. <br> 3. Encienda la impresora. <br> 4. Seleccione la boleta en la grilla del día y pulse `F9` (botón "Imprimir (F9)"). | Con la impresora apagada la boleta queda grabada y el mensaje informa el error de impresión e indica "Pulse F9 para reintentar". Con la impresora encendida, `F9` imprime la boleta. | | |

## Edición y anulación (UAT-EDI)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-EDI-01 | F6 | 1. Ingrese como Supervisor (o Administrador). <br> 2. Abra **Boletas › Buscar / editar boleta** (`F6`) y busque una boleta vigente. <br> 3. Selecciónela y pulse "Editar (F4)". <br> 4. Cambie un dato descriptivo (por ejemplo, la observación o el cliente) y revise que los pesos no se puedan modificar. <br> 5. Guarde con `F12`. <br> 6. Abra **Administración › Visor de auditoría** y revise el registro de la edición. | El editor no permite modificar los pesos ni el tipo de desposte. La auditoría muestra la diferencia entre el valor anterior y el nuevo. | | |
| UAT-EDI-02 | F6 | 1. En **Boletas › Buscar / editar boleta** seleccione una boleta de prueba vigente. <br> 2. Pulse "Anular (F8)" e indique un motivo de entre 5 y 100 caracteres; confirme. <br> 3. Reimprima la boleta anulada con `F3` (**Boletas › Reimprimir boleta**). <br> 4. Grabe una boleta nueva y compare su número con el de la anulada. | La boleta pasa a estado anulado. La reimpresión muestra la leyenda ANULADA con el motivo. El número de la boleta anulada no se reutiliza (la siguiente boleta tiene el número consecutivo siguiente). | | |

## Reportes (UAT-REP)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-REP-01 | F7 | 1. Abra **Reportes › Reportes de pesaje** (Tipo de reporte: Pesajes del día). <br> 2. Seleccione la fecha de trabajo y pulse "Generar (Enter)". <br> 3. Sume manualmente las boletas del día (media res cuenta como 0,5 res) y los pesos. <br> 4. Compare con los totales del reporte. | Los totales de peso y de reses del reporte coinciden con la suma manual (res completa = 1, media res = 0,5). | | |
| UAT-REP-02 | F7 | 1. En **Reportes › Reportes de pesaje** genere un reporte "Por ganadero" y otro "Por cliente". <br> 2. Genere un reporte "Entre fechas" con un rango que incluya varios días. <br> 3. Verifique que las boletas anuladas no aparezcan en esos reportes. <br> 4. Abra **Reportes › Boletas anuladas** y genere el reporte. | Cada reporte muestra solo las boletas del filtro elegido. Las boletas anuladas no se incluyen en los reportes de pesaje y solo aparecen en el reporte de boletas anuladas. | | |
| UAT-REP-03 | F7 | 1. En un reporte generado pulse "Exportar PDF…" y guarde el archivo. <br> 2. Abra el PDF. <br> 3. Pulse "Exportar Excel…", guarde y abra el archivo en Excel. <br> 4. Pulse "Vista previa" y luego "Imprimir…". | El PDF y el Excel se guardan y abren correctamente, con los mismos datos que el reporte en pantalla. La vista previa se muestra y la impresión sale por la impresora elegida. | | |
| UAT-REP-04 | F7 | 1. En **Reportes › Reportes de pesaje** elija "Entre fechas" con el rango más grande de datos disponible (por ejemplo, un mes). <br> 2. Anote la hora de inicio, pulse "Generar (Enter)" y anote cuánto tarda en mostrarse. <br> 3. Exporte ese reporte a PDF y anote el tiempo y si la PC responde con normalidad. | El reporte se genera en un tiempo aceptable para el cliente y la PC no se congela ni agota la memoria. Los tiempos y la cantidad de boletas quedan anotados en Observaciones. | | |

## Utilitarios (UAT-UTI)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-UTI-01 | F8 | 1. Conecte el pendrive y abra **Utilitarios › Backup / Restaurar**. <br> 2. En "Copiar también a (opcional)" use "Examinar…" para elegir una carpeta del pendrive y guarde la configuración. <br> 3. Pulse "Crear backup ahora". <br> 4. Revise la lista de backups y el contenido del pendrive. <br> 5. Anote la fecha y la última boleta del backup creado (se usará en UAT-UTI-03). | Se crea el backup manual (tipo manual) en la lista, con su número de boletas y última boleta. El mismo archivo `.zip` aparece en la carpeta del pendrive. | | |
| UAT-UTI-02 | F8 | 1. En **Utilitarios › Backup / Restaurar** marque "Crear un backup automático cada día" y "Crear también al cerrar el sistema"; guarde. <br> 2. Deje el sistema abierto más de 2 minutos y pulse "Actualizar (F5)". <br> 3. Cierre el sistema con **Sesión › Salir**. <br> 4. Vuelva a abrirlo y revise la lista de backups. | Después de los 2 minutos aparece el backup automático del día. Al cerrar el sistema se crea además el backup de cierre. Ambos figuran en la lista. | | |
| UAT-UTI-03 | F8 | 1. **Antes de la jornada piloto**, abra **Utilitarios › Backup / Restaurar**. <br> 2. Seleccione el backup creado en UAT-UTI-01 y pulse "Restaurar". <br> 3. Revise el resumen de la validación y confirme "Restaurar". <br> 4. Espere el reinicio automático e ingrese. <br> 5. Verifique que los datos corresponden a la fecha del backup (por ejemplo, última boleta). | El sistema valida el backup, se reinicia solo y los datos son los de la fecha del backup. Se genera un resguardo previo (tipo seguridad) antes de restaurar. | | |
| UAT-UTI-04 | F8 | 1. Abra **Utilitarios › Importar datos legados**. <br> 2. Seleccione el archivo de la muestra real del sistema anterior (CSV, DBF o Access). <br> 3. Revise el mapeo de columnas y la codificación (ñ y acentos se leen bien). <br> 4. Ejecute el simulacro y revise el informe. <br> 5. Ejecute la importación. <br> 6. Grabe una boleta nueva y verifique su número. | El simulacro informa filas válidas y errores sin modificar datos. Tras importar, los datos aparecen en los catálogos y el buscador de boletas, ñ y acentos son correctos, y la numeración de boletas continúa después de la mayor boleta importada. | | |
| UAT-UTI-05 | F8 | 1. Abra **Utilitarios › Visor de logs**. <br> 2. En "Nivel" elija "Solo errores" y revise el resultado. <br> 3. Cambie a "Todos" y escriba un texto conocido en "Buscar:". | El visor muestra el archivo de log del día, filtra por nivel y la búsqueda deja solo las entradas que contienen el texto. | | |

## Actualización (UAT-ACT)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-ACT-01 | F9-02/03 | 1. Con el sistema ya en uso (con boletas y catálogos), cierre el sistema. <br> 2. Ejecute el instalador de la misma versión (o de una nueva, cuando exista) sobre la instalación existente. <br> 3. Abra el sistema e ingrese. <br> 4. Verifique que ganaderos, clientes y boletas siguen presentes. <br> 5. Si la versión nueva incluía migraciones de base de datos, revise la carpeta de backups (`C:\ProgramData\PesajeFaeneo\backups`) y **Administración › Visor de auditoría**. | Los datos siguen intactos. Si hubo migraciones, existe un archivo `PesajeFaeneo_premigracion_*.zip` y la auditoría muestra la acción "ActualizacionSistema". Si no hubo migraciones, anotar "Sin migraciones" en Observaciones. | | |
| UAT-ACT-02 | F9-03 | 1. Con el sistema abierto, ejecute el instalador. <br> 2. Lea el mensaje del instalador. <br> 3. Cierre el sistema y vuelva a ejecutar el instalador. | El instalador detecta que el sistema está abierto, pide cerrarlo y no reemplaza archivos mientras siga abierto. | | |

## Desinstalación (UAT-DES)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-DES-01 | F9-03 | 1. Cierre el sistema. <br> 2. Desinstálelo desde "Aplicaciones instaladas" de Windows. <br> 3. Lea el aviso final del desinstalador. <br> 4. Revise que `C:\Program Files\PesajeFaeneo` desapareció y que `C:\ProgramData\PesajeFaeneo` sigue con su contenido. <br> 5. Vuelva a instalar el sistema y abra una boleta anterior en **Boletas › Buscar / editar boleta**. | El programa desaparece de Windows. El desinstalador avisa que los datos se conservan. `C:\ProgramData\PesajeFaeneo` queda intacto. Al reinstalar, los datos anteriores están disponibles. | | |

## Operación (UAT-OPE)

| ID | HU | Pasos | Resultado esperado | Resultado | Observaciones |
|---|---|---|---|---|---|
| UAT-OPE-01 | — | 1. Revise fecha, hora y zona horaria de Windows (Configuración › Hora e idioma). <br> 2. Grabe una boleta de prueba. <br> 3. Compare la fecha y hora impresas o mostradas en la boleta con el reloj de la PC. | La fecha, hora y zona horaria de Windows son las del sitio, y la hora de la boleta coincide con el reloj. | | |
| UAT-OPE-02 | — | 1. Durante un día de faena real, use el sistema en paralelo con el procedimiento anterior. <br> 2. Al terminar la jornada, genere **Reportes › Reportes de pesaje** (Pesajes del día). <br> 3. Compare los totales (cabezas y kg) con los del procedimiento anterior. <br> 4. Anote las diferencias y sus causas. | Los totales del sistema coinciden con los del procedimiento anterior. Toda diferencia queda explicada en Observaciones. | | |
