# Guía de instalación y soporte técnico

Esta guía es para el **técnico** que instala y da soporte, y para el **desarrollador** que publica versiones. Corresponde a la versión **1.0.0** del Sistema de Pesaje para Faeneo. Documentos relacionados: `manual-usuario.md` (para el personal), `guia-balanza.md` (balanza) y `docs/puesta-en-marcha/checklist-uat.md` (aceptación en sitio).

## 1. Requisitos

| Elemento | Requisito |
| :--- | :--- |
| Sistema operativo | Windows 10 (versión 1809 o superior) u 11, de **64 bits**. El instalador no corre en 32 bits ni en versiones anteriores. |
| Memoria | **8 GB de RAM recomendados**. Con 4 GB, los PDF de más de unas 5.000 boletas pueden quedarse sin memoria. |
| Disco | 1 GB libre, más el espacio para las copias de seguridad. |
| Balanza | Un puerto USB para el adaptador USB-serie, o red para indicadores TCP/IP. |
| Impresora | Con su controlador (driver) de Windows instalado. |
| Otros programas | **Ninguno.** El sistema es autocontenido: no hace falta instalar .NET ni Visual C++. |

La aplicación no verifica la memoria ni el espacio en disco. El instalador sí rechaza Windows de 32 bits o anterior a la versión 1809.

## 2. Instalación

1. Copie a la computadora el instalador `PesajeFaeneo-Setup-<versión>-x64.exe` y su archivo `.sha256`.
2. **Verifique el SHA-256** abriendo PowerShell en la carpeta del instalador:

   ```powershell
   Get-FileHash .\PesajeFaeneo-Setup-<versión>-x64.exe -Algorithm SHA256
   ```

   El valor mostrado debe ser igual al que contiene el archivo `.sha256`. Si no coincide, no instale: el archivo se dañó o fue modificado.
3. Ejecute el instalador **como administrador** (clic derecho › **Ejecutar como administrador**). Pedirá permisos de administrador.
4. El asistente está en español. La carpeta propuesta es `C:\Program Files\PesajeFaeneo`. Marque la opción de acceso directo en el escritorio si lo desea (siempre se crea un acceso en el menú Inicio, con el nombre "Sistema de Pesaje para Faeneo").
5. Al terminar, puede dejar marcada la casilla para abrir el sistema. Se abre con el usuario que instaló, no con privilegios elevados.

El instalador **no está firmado digitalmente**. Por eso Windows puede mostrar avisos:

- **SmartScreen** ("Windows protegió su PC"): pulse **Más información** y luego **Ejecutar de todas formas**.
- **Antivirus:** puede advertir o bloquear el instalador o el ejecutable. Si ocurre, confirme el SHA-256 y agregue una excepción para el instalador y para `C:\Program Files\PesajeFaeneo`.

El instalador copia también el archivo `LICENCIAS-TERCEROS.txt` a la carpeta del programa.

> [Captura: asistente del instalador en español, página de carpeta de destino]

## 3. Carpetas y permisos

| Carpeta | Contenido |
| :--- | :--- |
| `C:\Program Files\PesajeFaeneo` | El programa (ejecutable, bibliotecas y `LICENCIAS-TERCEROS.txt`). |
| `C:\ProgramData\PesajeFaeneo` | Los **datos**: `pesaje.db` (base de datos), `config` (configuración de impresión y de backup de este equipo), `logs`, `logo`, `backups` y `restore`. |

La carpeta de datos **no se pierde ni se reemplaza** al actualizar ni al desinstalar.

### Permisos

El instalador crea `C:\ProgramData\PesajeFaeneo` y sus subcarpetas con permiso **Modificar para el grupo Usuarios** y además ajusta con `icacls` los archivos que hubiera creado una versión anterior (usa el identificador de grupo, no el nombre, para que funcione en Windows en cualquier idioma).

El motivo: cualquier usuario de Windows del equipo que abra el sistema necesita escribir la base de datos, los logs y las copias. Sin ese permiso, un usuario estándar de Windows no podría usarlo.

### Consecuencia de seguridad

**Cualquier usuario de Windows de esa computadora puede leer y copiar esos archivos**, incluidas la base de datos y las copias de seguridad (que contienen los hashes de las contraseñas del sistema). El sistema está pensado para un equipo único con personal de confianza. Recomendaciones:

- Use cuentas de Windows solo para el personal autorizado en esa PC.
- No deje las copias de seguridad en lugares accesibles a otras personas; custodie los pendrives.

Si un antivirus bloquea `pesaje.db` o genera errores de acceso intermitentes, agregue una exclusión para `C:\ProgramData\PesajeFaeneo`.

## 4. Primer arranque

1. Abra **Sistema de Pesaje para Faeneo**. En la primera ejecución el sistema crea la base de datos en `C:\ProgramData\PesajeFaeneo`.
2. Ingrese con el usuario **`admin`** y la contraseña **`admin`**.
3. El sistema obliga a **cambiar la contraseña** antes de continuar: mínimo 6 caracteres y distinta de `admin`. Anótela y entréguela al responsable.
4. Se abre el **Asistente de puesta en marcha** (solo para el Administrador, mientras no se haya finalizado). Guía por siete pasos: copias de seguridad / restaurar desde otro equipo (opcional), datos de la empresa (**obligatorio**), balanza (**obligatorio**), impresión (recomendado), fleteros (opcional), usuarios (recomendado) e importación de datos del sistema anterior (opcional).
   - La balanza viene configurada como **Simulador**. Para finalizar hay que configurar una balanza real, o marcar la casilla que confirma el uso del simulador (solo pruebas).
   - Puede cerrarlo con **Omitir por ahora**: volverá a aparecer en cada ingreso del Administrador hasta que se finalice. Se reabre desde **Ayuda › Asistente de puesta en marcha…**.
   - En equipos donde el sistema ya se usaba, el asistente aparece una vez con los pasos ya marcados como listos.
5. Cree los usuarios del personal (Operador, Supervisor) y no comparta la cuenta de administrador.
6. Complete el checklist de aceptación en sitio: `docs/puesta-en-marcha/checklist-uat.md`.

## 5. Actualizar a una versión nueva

1. **Cierre el sistema** en todas las sesiones de Windows (**Sesión › Salir**). Si queda abierto, el instalador lo detecta, pide cerrarlo y no reemplaza archivos.
2. Ejecute el instalador de la versión nueva (como administrador). Se instala **encima**: mismo programa, misma carpeta, y los datos no se tocan.
3. Abra el sistema. Si la versión nueva trae cambios en la base de datos, al abrirse el sistema:
   - Crea primero una copia llamada `PesajeFaeneo_premigracion_<aaaa-mm-dd_HHmmss>.zip` en `C:\ProgramData\PesajeFaeneo\backups\seguridad` (se conservan las 10 más recientes).
   - Actualiza la base de datos automáticamente.
   - Deja una fila de auditoría "ActualizacionSistema" (visible en **Administración › Visor de auditoría**, con la versión, las migraciones y el nombre de la copia).
4. Si la versión nueva no cambia la base de datos, no se crea esa copia.

**Guarde siempre los instaladores anteriores** (con su `.sha256`): el procedimiento de vuelta atrás de la sección 6 los necesita.

## 6. Si la actualización de la base de datos falla

Si algo impide actualizar la base de datos, el sistema **no abre**: muestra un cuadro titulado **"No se pudo abrir la base de datos"** y termina con código de salida 2. Hay tres mensajes:

| Mensaje | Qué significa | Qué hacer |
| :--- | :--- | :--- |
| "La base de datos fue actualizada por una versión más nueva del sistema. Instale esa versión (o una posterior) para seguir trabajando. No se modificó ningún dato." | Se instaló una versión **anterior** sobre datos de una versión más nueva. El sistema no copia ni modifica nada. | Instale la **versión más nueva** (la que se usaba antes). **No restaure nada.** |
| "No se pudo crear la copia de seguridad previa a la actualización de la base de datos: … La base de datos no se modificó. Libere espacio en el disco o contacte a soporte." | La copia de seguridad previa falló (por ejemplo, disco lleno). Por seguridad **no se actualizó nada**. | Libere espacio en disco y abra el sistema de nuevo. Si persiste, contacte a soporte con el log del día. |
| "La actualización de la base de datos falló. Sus datos quedaron resguardados en la copia … (carpeta backups\seguridad). No use el sistema: contacte a soporte para restaurarla." | La actualización falló a la mitad: la base puede haber quedado a medio actualizar. Los datos anteriores están en la copia previa. **No hay vuelta atrás automática.** | Siga el procedimiento siguiente. |

### Procedimiento de vuelta atrás (actualización fallida)

1. **No abra el sistema.**
2. Copie la carpeta completa `C:\ProgramData\PesajeFaeneo` a un pendrive (es el estado actual, por si acaso).
3. Instale encima el instalador de la versión **anterior**.
4. En `C:\ProgramData\PesajeFaeneo\backups\seguridad`, abra el `PesajeFaeneo_premigracion_<fecha>.zip` más reciente y extraiga `pesaje.db`.
5. Con el sistema cerrado, borre `pesaje.db`, `pesaje.db-wal` y `pesaje.db-shm` de `C:\ProgramData\PesajeFaeneo` y copie allí el `pesaje.db` extraído.
6. Abra el sistema y compruebe la última boleta.
7. Envíe a soporte el log del día (`C:\ProgramData\PesajeFaeneo\logs\pesaje-<aaaammdd>.log`).

## 7. Copias de seguridad y restauración

Se administran desde **Utilitarios › Backup / Restaurar** (rol Administrador). El uso paso a paso está en el manual de usuario (sección 10). Aquí, lo que importa a soporte.

### Dónde están y cómo se llaman

`C:\ProgramData\PesajeFaeneo\backups`, con el nombre `PesajeFaeneo_{tipo}_{yyyy-MM-dd[_HHmmss]}.zip`:

| Carpeta | Archivos | Quién los crea |
| :--- | :--- | :--- |
| `manuales` | `PesajeFaeneo_manual_<fecha_hora>.zip` | El usuario, con **Crear backup ahora**. |
| `automaticos` | `PesajeFaeneo_automatico_<fecha>.zip` (uno por día) | El sistema, a diario y, si está activado, al cerrar. |
| `seguridad` | `PesajeFaeneo_prerestauracion_…`, `PesajeFaeneo_preimportacion_…`, `PesajeFaeneo_premigracion_…` | El sistema, antes de restaurar, importar o actualizar de versión. Se conservan los 10 más recientes de cada tipo. |

### Qué contiene cada copia

- `manifest.json`: versión del sistema, fecha, equipo de origen, cantidad de boletas, última boleta, ganaderos, clientes, usuarios activos y suma de verificación.
- `pesaje.db`: la base de datos completa.
- Carpeta `logo/`: el logotipo de la empresa.
- **No incluye** `logs` ni la carpeta `config` (la configuración de impresión y de backup es propia de cada equipo).

> **Seguridad:** la base de datos incluye la tabla de usuarios con las **contraseñas cifradas (hashes)**. Toda copia contiene, por lo tanto, información sensible: custodie los pendrives y no deje copias en lugares accesibles.

### Retención y copia secundaria

- Backups automáticos: se conservan entre 7 y 365 días (30 por defecto) y siempre las 3 más recientes.
- **Copia secundaria:** el Administrador puede indicar un pendrive o una carpeta de red. Cada copia se duplica allí; si el destino no está disponible, la copia local se hace igual y se muestra una advertencia. Dos computadoras que usen la misma carpeta secundaria de red pueden pisarse las copias automáticas del mismo día.
- La carpeta `restore` guarda las bases reemplazadas por una restauración (`reemplazada-<fecha>`, se conservan las 3 últimas) y el marcador de la restauración pendiente.

### Restaurar

Desde **Utilitarios › Backup / Restaurar**: **Restaurar seleccionada…** o **Restaurar desde archivo…**, escribir `RESTAURAR` y confirmar. El sistema valida la copia, guarda un resguardo de la base actual, se cierra y se reabre solo. La restauración se aplica al abrirse, antes de abrir la base de datos; si la copia restaurada no abre, vuelve sola a la base anterior.

## 8. Restaurar a mano cuando el sistema no abre

Use este procedimiento si el sistema no abre y no se puede usar la pantalla de restauración.

1. **No abra el sistema.**
2. Copie la carpeta completa `C:\ProgramData\PesajeFaeneo` a un pendrive (estado actual, por si acaso).
3. En `C:\ProgramData\PesajeFaeneo\backups`, elija la copia que desea recuperar de `automaticos`, `manuales` o `seguridad` (la más reciente anterior al problema, salvo que soporte indique otra) y abra el `.zip`. Extraiga `pesaje.db`.
4. Con el sistema cerrado, borre `pesaje.db`, `pesaje.db-wal` y `pesaje.db-shm` de `C:\ProgramData\PesajeFaeneo` y copie allí el `pesaje.db` extraído.
5. Si la carpeta `C:\ProgramData\PesajeFaeneo\logo` no tiene los archivos de imagen, copie en ella los archivos de la carpeta `logo/` del zip.
6. Abra el sistema y compruebe la última boleta.

Notas:

- Si la copia es de una versión anterior, el sistema actualizará la base al abrirse (y creará su propia copia previa, ver sección 5).
- La copia no trae la configuración de impresión ni la de backup de la computadora original; en otra PC hay que configurarlas de nuevo.

## 9. Recuperar el acceso del administrador

Si se perdió la contraseña del administrador y no hay otro administrador que pueda restablecerla desde **Administración › Usuarios**, se puede restablecer la cuenta `admin` desde la computadora. Solo funciona con el proceso **elevado como administrador de Windows**.

1. **Cierre el sistema** (si está abierto en cualquier sesión, el comando responderá "ya está en ejecución").
2. Abra una consola de **PowerShell como administrador** (clic derecho en PowerShell › **Ejecutar como administrador**). No use "Abrir la ubicación del archivo" y "Ejecutar como administrador" desde el acceso directo, porque así no se pueden pasar argumentos al programa.
3. Ejecute:

   ```powershell
   & "C:\Program Files\PesajeFaeneo\PesajeFaeneo.WinForms.exe" --restablecer-admin
   ```

4. Si no está elevado, aparece el aviso "Para restablecer la cuenta de administrador, ejecute el sistema como administrador de Windows…" y el programa termina con código 3 sin hacer nada.
5. Si está elevado, aparece una confirmación: "Se restablecerá la cuenta 'admin' con una contraseña temporal nueva. Quien la use deberá cambiarla al ingresar. ¿Continuar?". Pulse **Sí**.
6. Se muestra la **contraseña temporal una sola vez**. Anótela: no se vuelve a mostrar y no queda registrada ni en el log ni en la auditoría.
7. Abra el sistema normalmente, ingrese con `admin` y la contraseña temporal, y cambie la contraseña cuando el sistema lo exija.

Qué hace: deja la cuenta `admin` **activa**, con rol **Administrador**, **desbloqueada** y con contraseña temporal. Si la cuenta `admin` no existe, la crea. El comando no abre la aplicación. Queda registrado en la auditoría (acción "RestablecerPassword") y en el log, sin la contraseña.

## 10. Motor de Access para importar archivos .mdb y .accdb

Importar archivos de Access desde **Utilitarios › Importar datos legados** requiere el proveedor de datos de Access (ACE) de **64 bits**. No viene con el sistema ni con Windows.

1. Descargue del sitio de Microsoft el **"Microsoft Access Database Engine 2016 Redistributable" de 64 bits**.
2. Instálelo.
3. Si en la computadora hay un **Office de 32 bits** instalado, el instalador del motor de 64 bits se niega a continuar. En ese caso, ejecute el instalador del motor **con el parámetro `/quiet`** desde una **consola de administrador** (por ejemplo, `<instalador-del-motor> /quiet`, indicando la ruta del archivo descargado).
4. Si no puede o no quiere instalarlo, la **alternativa** es abrir el archivo en Access y **exportar las tablas a CSV**, y luego importar los CSV (el importador acepta CSV y TXT).

## 11. Impresoras

- **Impresoras matriciales:** instale el **controlador del fabricante**. Los controladores genéricos de Windows pueden dar impresión lenta o con letras dentadas. Esta versión imprime siempre por el controlador de Windows (no hay modo de texto directo para matriciales). Ajuste la alineación con la **página de calibración** y los desplazamientos de **Administración › Configuración de impresión**.
- **Impresoras virtuales** ("Microsoft Print to PDF", "XPS Document Writer", fax): piden un nombre de archivo en cada impresión. **Evítelas con la impresión automática**; el sistema muestra una advertencia en la configuración.
- **La vista previa necesita al menos una impresora instalada.** En una computadora sin ninguna impresora, la vista previa de una boleta o de un reporte puede fallar. El PDF del reporte (**Exportar PDF…**) sigue disponible.
- **La configuración de impresión es propia de cada equipo** (se guarda en `C:\ProgramData\PesajeFaeneo\config\impresion.json`, atada al nombre de la computadora). Si se cambia el nombre de la PC, o si se restaura una copia de otro equipo, hay que volver a configurarla.
- Si la impresora configurada no está instalada, la configuración conserva su nombre y avisa "La impresora configurada no está instalada en este equipo."

## 12. Diagnóstico

### Logs

- Carpeta: `C:\ProgramData\PesajeFaeneo\logs`. Un archivo por día, `pesaje-<aaaammdd>.log`; se conservan 30 días.
- Desde el sistema: **Utilitarios › Visor de logs** (Administrador): filtro por nivel (por ejemplo "Solo errores") y búsqueda de texto; **Abrir carpeta** abre la carpeta de logs.
- Al arrancar, el log del día registra una línea con la versión: "Aplicación iniciada v… (versión+compilación)".
- Los logs no contienen contraseñas.

### Autoprueba de la instalación

Comprueba que la instalación está completa, **sin tocar los datos reales**: usa una carpeta de datos temporal, que debe ser **nueva o estar vacía**, con ruta completa y fuera de `C:\ProgramData\PesajeFaeneo`. No abre ventanas.

```powershell
$p = Start-Process "C:\Program Files\PesajeFaeneo\PesajeFaeneo.WinForms.exe" -ArgumentList '--autoprueba "C:\Temp\autoprueba-uat"' -Wait -PassThru
$p.ExitCode
Get-Content "C:\Temp\autoprueba-uat\autoprueba.json"
```

(Es el caso UAT-INS-06 del checklist; también sirve la forma directa `& "C:\Program Files\PesajeFaeneo\PesajeFaeneo.WinForms.exe" --autoprueba "C:\Temp\autoprueba-uat"`.)

- Código de salida **0**: todo bien. **1**: algún paso falló. **3**: la carpeta no es válida (no es ruta completa, es la de datos reales o no está vacía).
- El archivo `autoprueba.json` queda en esa carpeta. Debe decir `"Exito": true`.
- Revisa cinco cosas, cada una por separado: `BaseDeDatos` (crea y migra una base de prueba), `Pdf` (genera un PDF real), `Excel` (genera un Excel real), `Backup` (crea una copia) y `Escritura` (la carpeta de datos permite escribir). Si un paso falla, su mensaje en el JSON dice la causa; los demás se siguen ejecutando.
- Cuando termine, borre la carpeta de prueba.

### Datos para soporte

En **Ayuda › Acerca de…**, el botón **Copiar para soporte** copia al portapapeles: versión, compilación, versión de .NET, 32 o 64 bits, nombre del equipo, carpeta de datos y carpeta del programa. Péguelo en el correo o ticket junto con el log del día.

## 13. Desinstalar

1. Cierre el sistema.
2. Abra el Panel de control (o Configuración) › **Aplicaciones**, busque **Sistema de Pesaje para Faeneo** y desinstale.
3. Al terminar, aparece un aviso: los datos del sistema (base de datos, copias de seguridad y configuración) **se conservaron** en `C:\ProgramData\PesajeFaeneo`.

La desinstalación **nunca borra los datos**. Reinstalar el sistema recupera los datos existentes.

Para borrar los datos definitivamente (por ejemplo, al retirar la computadora), elimine a mano `C:\ProgramData\PesajeFaeneo` **después** de guardar un backup (por ejemplo, en un pendrive).

## 14. Procedimiento de release (desarrollo)

Para publicar una versión nueva:

1. Subir `<Version>` en `Directory.Build.props` según estas reglas (SemVer `MAYOR.MENOR.PARCHE`): **PARCHE** = correcciones sin cambios en la base de datos; **MENOR** = funciones nuevas o migraciones; **MAYOR** = cambios incompatibles con copias de seguridad anteriores. Es la **única fuente** de la versión; no la escriba en otro lado.
2. Actualizar `CHANGELOG.md`.
3. Ejecutar `.\build.ps1` (compila y corre todas las pruebas; debe terminar sin fallos).
4. Ejecutar:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\construir-instalador.ps1
   ```

   Publica la aplicación autocontenida (necesita acceso a internet para descargar los paquetes de ejecución, y verifica el resultado con la autoprueba) y compila el instalador con **Inno Setup 6.3 o superior** (instalación: `winget install --id JRSoftware.InnoSetup -e`). Deja `PesajeFaeneo-Setup-<versión>-x64.exe` y su `.sha256` en `artifacts\instalador`.
5. Guardar el `.exe` y su `.sha256` en el archivo de entregas.
6. Hacer commit y crear la etiqueta `v<versión>` (git).
7. Si se agregó una migración, **probar la actualización sobre una copia de la base de datos de producción** antes de entregar.

Reglas que no se deben romper:

- El `AppId` del instalador (`instalador\PesajeFaeneo.iss`) es **fijo para siempre**: si cambia, la versión nueva se instalaría al lado de la anterior.
- No agregar `RuntimeIdentifier` ni `SelfContained` a los `.csproj`: la publicación solo se configura en `scripts\publicar.ps1`.
- Los archivos `scripts\*.ps1` e `instalador\*.iss` deben ser ASCII puro (sin tildes ni eñes).
- Conservar los instaladores entregados (la vuelta atrás de la sección 6 los necesita).

## 15. Limitaciones conocidas

- **Licencia de QuestPDF:** los PDF se generan con QuestPDF bajo su licencia **Community**, que tiene condiciones para el uso comercial. El cliente debe confirmar que las cumple.
- **Sin firma de código:** el instalador y el ejecutable no están firmados; SmartScreen o el antivirus pueden advertir (sección 2).
- **PDF de muchas boletas y memoria:** generar un PDF de 20.000 boletas (el máximo) puede consumir varios GB de memoria. Con 4 a 8 GB de RAM hay riesgo de quedarse sin memoria. Recomendación: acortar el rango de fechas y usar equipos de 8 GB de RAM (sección 1).
- **Una sola instancia por equipo:** no se puede abrir el sistema dos veces en la misma computadora, ni en dos sesiones de Windows a la vez.
- **Datos legibles por los usuarios locales:** cualquier usuario de Windows del equipo puede leer la base y las copias (sección 3).
- **Sin vuelta atrás automática:** si una actualización de la base de datos falla, la recuperación es manual (sección 6).
- **Sin actualización automática:** las versiones nuevas se instalan a mano.
- **Una balanza por equipo, solo transmisión continua:** no se soportan indicadores que solo responden bajo pedido.
- **Solo Windows de 64 bits.**
- **Reinicio tras restaurar:** el reinicio automático solo funciona con el programa instalado (no ejecutando desde el entorno de desarrollo).
- **Importación desde Access:** requiere el motor ACE de 64 bits (sección 10).
- **Antivirus:** si bloquea `pesaje.db` o el ejecutable, agregue exclusiones para `C:\ProgramData\PesajeFaeneo` y `C:\Program Files\PesajeFaeneo`.
